using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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

        // Guesty Booking Engine API has strict rate limits (e.g., ~5 req/sec).
        // We apply a lightweight global throttle + 429 retry to avoid flakiness.
        private static readonly System.Threading.SemaphoreSlim _rateGate = new(1, 1);
        private static DateTime _lastRequestUtc = DateTime.MinValue;
        // 260ms ~= 3.8 req/sec (~228 req/min), staying comfortably under Guesty's minute limit.
        private static readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(260);

        public GuestyOpenApiClient(HttpClient http, Microsoft.Extensions.Options.IOptions<GuestyOptions> options)
        {
            _http = http;
            _options = options.Value ?? new GuestyOptions();
        }

        private static async Task ThrottleAsync()
        {
            await _rateGate.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastRequestUtc;
                if (elapsed < _minInterval)
                {
                    var delay = _minInterval - elapsed;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay);
                }
                _lastRequestUtc = DateTime.UtcNow;
            }
            finally
            {
                _rateGate.Release();
            }
        }

        private static bool IsRetryable(HttpStatusCode code)
        {
            // Guesty can occasionally return transient 5xx for specific listings.
            // We retry a few times to reduce flakiness.
            if (code == (HttpStatusCode)429) return true;

            var n = (int)code;
            return n == 500 || n == 502 || n == 503 || n == 504;
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory)
        {
            HttpResponseMessage? last = null;

            for (var attempt = 0; attempt < 4; attempt++)
            {
                await ThrottleAsync();

                using var req = requestFactory();
                last = await _http.SendAsync(req);

                if (!IsRetryable(last.StatusCode))
                    return last;

                // Respect Retry-After if present (mostly for 429), otherwise do a small exponential backoff.
                TimeSpan? retryAfter = null;
                if (last.StatusCode == (HttpStatusCode)429)
                {
                    retryAfter = last.Headers.RetryAfter?.Delta;
                    if (!retryAfter.HasValue && last.Headers.RetryAfter?.Date.HasValue == true)
                    {
                        var delta = last.Headers.RetryAfter!.Date!.Value - DateTimeOffset.UtcNow;
                        if (delta > TimeSpan.Zero) retryAfter = delta;
                    }
                }

                var backoff = retryAfter ?? TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
                if (backoff > TimeSpan.FromSeconds(10)) backoff = TimeSpan.FromSeconds(10);
                await Task.Delay(backoff);
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
            // Booking Engine API uses cursor-based pagination for /listings (no `skip`).
            // Guesty validates `limit <= 100`.
            var pageSize = Math.Clamp(limit, 1, 100);

            var all = new List<GuestyListingDTO>();
            string? next = cursor;

            // Safety valve: if something goes weird with pagination, don't loop forever.
            const int maxPages = 200; // safety valve (200*100=20k max)
            for (var page = 0; page < maxPages; page++)
            {
                var (items, nextCursor) = await GetListingsPageAsync(accessToken, pageSize, next, city, status);
                if (items.Count > 0)
                    all.AddRange(items);

                if (string.IsNullOrWhiteSpace(nextCursor))
                    break;

                // If API returns the same cursor again, break to avoid an infinite loop.
                if (!string.IsNullOrWhiteSpace(next) && string.Equals(next, nextCursor, StringComparison.Ordinal))
                    break;

                next = nextCursor;
            }

            // Distinct by Id (defensive)
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

            // Optional (best-effort) filters:
            if (!string.IsNullOrWhiteSpace(city)) query["city"] = city;
            if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;

            var req = CreateRequest(HttpMethod.Get, "/listings" + BuildQuery(query), accessToken);
            var res = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Get, "/listings" + BuildQuery(query), accessToken));
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
            // Booking Engine API calendar is per-listing: GET /listings/{listingId}/calendar?from=...&to=...
            // To keep our backend API stable, we aggregate results into a JSON array:
            // [ { "listingId": "...", "calendar": <raw calendar json> }, ... ]
            if (listingIds == null)
                return "[]";

            var ids = listingIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
            if (ids.Count == 0)
                return "[]";

            // Guesty Booking Engine API uses query params: from/to (YYYY-MM-DD)
            // Ref: https://booking-api-docs.guesty.com/reference/getcalendarbylistingid
            var query = new Dictionary<string, string?>
            {
                ["from"] = startDate,
                ["to"] = endDate,
            };

            var wrapper = new List<Dictionary<string, object?>>();
            var successCount = 0;

            // IMPORTANT: do NOT blast hundreds of requests concurrently; Guesty rate limits hard.
            foreach (var id in ids)
            {
                var path = $"/listings/{Uri.EscapeDataString(id)}/calendar" + BuildQuery(query);
                var res = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Get, path, accessToken));

                if (!res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync();

                    // Best-effort: a single listing can occasionally fail with 5xx on Guesty.
                    // We skip it so the whole schedule doesn't fail.
                    // If *all* listings fail, we will throw at the end.
                    wrapper.Add(new Dictionary<string, object?>
                    {
                        ["listingId"] = id,
                        ["error"] = new
                        {
                            status = (int)res.StatusCode,
                            reason = res.ReasonPhrase,
                            body
                        }
                    });
                    continue;
                }

                var calendarJson = await res.Content.ReadAsStringAsync();

                object? calendarObj = null;
                try
                {
                    calendarObj = JsonSerializer.Deserialize<object>(calendarJson);
                }
                catch
                {
                    // fallback to raw string
                    calendarObj = calendarJson;
                }

                wrapper.Add(new Dictionary<string, object?>
                {
                    ["listingId"] = id,
                    ["calendar"] = calendarObj
                });

                successCount++;
            }

            if (successCount == 0)
            {
                // Preserve previous behavior if nothing could be fetched.
                // Include the first error body to help debugging.
                var firstErr = wrapper.FirstOrDefault(w => w.ContainsKey("error"));
                var errJson = firstErr != null ? JsonSerializer.Serialize(firstErr["error"]) : "unknown";
                throw new BadGatewayException($"Guesty API error while fetching calendar: all listings failed. {errJson}");
            }

            return JsonSerializer.Serialize(wrapper);
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

                    // Pagination cursor (best-effort): { pagination: { cursor: { next: "..." } } }
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
                // ignore and return empty
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
