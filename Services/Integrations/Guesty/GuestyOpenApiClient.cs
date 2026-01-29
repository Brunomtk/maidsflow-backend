using System;
using System.Collections.Generic;
using System.Linq;
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

        public GuestyOpenApiClient(HttpClient http, Microsoft.Extensions.Options.IOptions<GuestyOptions> options)
        {
            _http = http;
            _options = options.Value ?? new GuestyOptions();
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
            // Docs indicate `limit` has a fairly small max (commonly 25). We'll clamp to avoid 400s.
            var pageSize = Math.Clamp(limit, 1, 25);

            var all = new List<GuestyListingDTO>();
            string? next = cursor;

            // Safety valve: if something goes weird with pagination, don't loop forever.
            const int maxPages = 200; // 200 * 25 = 5000 listings
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
            var res = await _http.SendAsync(req);
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

    foreach (var id in ids)
    {
        var path = $"/listings/{Uri.EscapeDataString(id)}/calendar" + BuildQuery(query);
        var req = CreateRequest(HttpMethod.Get, path, accessToken);
        var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                throw new BadGatewayException($"Guesty API error while fetching calendar for listing '{id}': {(int)res.StatusCode} ({res.ReasonPhrase}). {body}");
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
