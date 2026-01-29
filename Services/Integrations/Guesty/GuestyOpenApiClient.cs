using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.Guesty;
using Core.Exceptions;
using Core.Options;

namespace Services.Integrations.Guesty
{
    public class GuestyOpenApiClient : IGuestyOpenApiClient
    {
        private readonly HttpClient _http;
        private readonly GuestyOptions _options;
        private readonly IGuestyRateLimiter _rateLimiter;

        public GuestyOpenApiClient(
            HttpClient http,
            Microsoft.Extensions.Options.IOptions<GuestyOptions> options,
            IGuestyRateLimiter rateLimiter)
        {
            _http = http;
            _options = options.Value ?? new GuestyOptions();
            _rateLimiter = rateLimiter;
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct = default)
        {
            HttpResponseMessage? last = null;

            for (var attempt = 0; attempt < 4; attempt++)
            {
                await _rateLimiter.AcquireAsync(ct);
                using var req = requestFactory();
                last = await _http.SendAsync(req, ct);

                if (last.IsSuccessStatusCode)
                    return last;

                var status = (int)last.StatusCode;
                var shouldRetry = status == 429 || (status >= 500 && status <= 599);
                if (!shouldRetry)
                    return last;

                TimeSpan? retryAfter = null;
                if (status == 429)
                {
                    retryAfter = last.Headers.RetryAfter?.Delta;
                    if (!retryAfter.HasValue && last.Headers.RetryAfter?.Date.HasValue == true)
                    {
                        var delta = last.Headers.RetryAfter!.Date!.Value - DateTimeOffset.UtcNow;
                        if (delta > TimeSpan.Zero) retryAfter = delta;
                    }
                }

                var backoff = retryAfter ?? TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt));
                if (backoff > TimeSpan.FromSeconds(8)) backoff = TimeSpan.FromSeconds(8);

                await Task.Delay(backoff, ct);
            }

            return last!;
        }

        private static string BuildQuery(Dictionary<string, string?> query)
        {
            var parts = new List<string>();
            foreach (var kv in query)
            {
                if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
            }
            return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, string accessToken)
        {
            var baseUrl = (_options.OpenApiBaseUrl ?? "https://booking.guesty.com/api").TrimEnd('/');
            var url = baseUrl + (path.StartsWith("/") ? path : "/" + path);

            var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return req;
        }

        public async Task<List<GuestyListingDTO>> GetListingsAsync(
            string accessToken,
            int limit = 25,
            string? cursor = null,
            string? city = null,
            string? status = null)
        {
            var pageSize = Math.Clamp(limit, 1, 100);

            var all = new List<GuestyListingDTO>();
            string? next = cursor;

            const int maxPages = 200;
            for (var page = 0; page < maxPages; page++)
            {
                var (items, nextCursor) = await GetListingsPageAsync(accessToken, pageSize, next, city, status);
                if (items.Count > 0)
                    all.AddRange(items);

                if (string.IsNullOrWhiteSpace(nextCursor))
                    break;
                if (!string.IsNullOrWhiteSpace(next) && string.Equals(next, nextCursor, StringComparison.Ordinal))
                    break;

                next = nextCursor;
            }

            return all
                .Where(l => !string.IsNullOrWhiteSpace(l.Id))
                .GroupBy(l => l.Id)
                .Select(g => g.First())
                .ToList();
        }

        private async Task<(List<GuestyListingDTO> Items, string? NextCursor)> GetListingsPageAsync(
            string accessToken,
            int pageSize,
            string? cursor,
            string? city,
            string? status)
        {
            var query = new Dictionary<string, string?>
            {
                ["limit"] = pageSize.ToString(),
                ["cursor"] = cursor,
            };

            if (!string.IsNullOrWhiteSpace(city)) query["city"] = city;
            if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;

            var path = "/listings" + BuildQuery(query);
            var res = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Get, path, accessToken));
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                throw new BadGatewayException($"Guesty API error while fetching listings: {(int)res.StatusCode} ({res.ReasonPhrase}). {body}");
            }

            var json = await res.Content.ReadAsStringAsync();
            return ParseListingsPage(json);
        }

        public async Task<string> GetCalendarRawAsync(string accessToken, string startDate, string endDate, IEnumerable<string>? listingIds = null)
        {
            if (listingIds == null)
                return "[]";

            var ids = listingIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
            if (ids.Count == 0)
                return "[]";

            var query = new Dictionary<string, string?>
            {
                ["from"] = startDate,
                ["to"] = endDate,
            };

            // Parallel fetch with bounded concurrency. The rate limiter keeps us under 5 req/s.
            var maxConcurrency = 6; // concurrency is OK; limiter governs actual request rate
            var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var wrapper = new ConcurrentBag<Dictionary<string, object?>>();

            await Task.WhenAll(ids.Select(async id =>
            {
                await gate.WaitAsync();
                try
                {
                    var path = $"/listings/{Uri.EscapeDataString(id)}/calendar" + BuildQuery(query);
                    var res = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Get, path, accessToken));

                    if (!res.IsSuccessStatusCode)
                    {
                        var body = await res.Content.ReadAsStringAsync();
                        wrapper.Add(new Dictionary<string, object?>
                        {
                            ["listingId"] = id,
                            ["error"] = new { status = (int)res.StatusCode, reason = res.ReasonPhrase, body }
                        });
                        return;
                    }

                    var calendarJson = await res.Content.ReadAsStringAsync();
                    object? calendarObj;
                    try
                    {
                        calendarObj = JsonSerializer.Deserialize<object>(calendarJson);
                    }
                    catch
                    {
                        calendarObj = calendarJson;
                    }

                    wrapper.Add(new Dictionary<string, object?>
                    {
                        ["listingId"] = id,
                        ["calendar"] = calendarObj
                    });
                }
                catch (Exception ex)
                {
                    wrapper.Add(new Dictionary<string, object?>
                    {
                        ["listingId"] = id,
                        ["error"] = ex.Message
                    });
                }
                finally
                {
                    gate.Release();
                }
            }));

            var anySuccess = wrapper.Any(x => x.ContainsKey("calendar") && x["calendar"] != null);
            if (!anySuccess)
            {
                var firstErr = wrapper.FirstOrDefault(x => x.ContainsKey("error"));
                throw new BadGatewayException($"Guesty calendar fetch failed for all listings. First error: {JsonSerializer.Serialize(firstErr)}");
            }

            var ordered = wrapper
                .OrderBy(x => x.TryGetValue("listingId", out var v) ? v?.ToString() : "")
                .ToList();

            return JsonSerializer.Serialize(ordered);
        }

        private static (List<GuestyListingDTO> Items, string? NextCursor) ParseListingsPage(string json)
        {
            var result = new List<GuestyListingDTO>();
            string? nextCursor = null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                JsonElement arr;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                        result.Add(ParseListingItem(item));

                    if (root.TryGetProperty("pagination", out var pag) && pag.ValueKind == JsonValueKind.Object)
                    {
                        if (pag.TryGetProperty("cursor", out var cur) && cur.ValueKind == JsonValueKind.Object)
                        {
                            if (cur.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String)
                                nextCursor = next.GetString();
                        }
                    }

                    return (result, nextCursor);
                }

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                        result.Add(ParseListingItem(item));
                    return (result, null);
                }
            }
            catch
            {
                // ignore
            }

            return (result, null);
        }

        private static GuestyListingDTO ParseListingItem(JsonElement item)
        {
            string id = "";
            if (item.TryGetProperty("_id", out var idEl)) id = idEl.ToString();
            else if (item.TryGetProperty("id", out var idEl2)) id = idEl2.ToString();

            string? nickname = item.TryGetProperty("nickname", out var n) ? n.GetString() : null;
            string? title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            string? status = item.TryGetProperty("status", out var s) ? s.ToString() : null;

            string? city = null;
            string? state = null;
            if (item.TryGetProperty("address", out var addr) && addr.ValueKind == JsonValueKind.Object)
            {
                if (addr.TryGetProperty("city", out var c)) city = c.GetString();
                if (addr.TryGetProperty("state", out var st)) state = st.GetString();
            }

            string? pictureUrl = null;
            if (item.TryGetProperty("picture", out var pic))
            {
                if (pic.ValueKind == JsonValueKind.String) pictureUrl = pic.GetString();
                else if (pic.ValueKind == JsonValueKind.Object)
                {
                    if (pic.TryGetProperty("thumbnail", out var th)) pictureUrl = th.GetString();
                    else if (pic.TryGetProperty("url", out var u)) pictureUrl = u.GetString();
                }
            }

            return new GuestyListingDTO
            {
                Id = id,
                Nickname = nickname,
                Title = title,
                City = city,
                State = state,
                PictureUrl = pictureUrl,
                Status = status
            };
        }
    }
}
