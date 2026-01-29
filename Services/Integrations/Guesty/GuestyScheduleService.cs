using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Core.DTO.Guesty;
using Core.Exceptions;

namespace Services.Integrations.Guesty
{
    public class GuestyScheduleService : IGuestyScheduleService
    {
        private readonly IGuestyIntegrationService _integration;
        private readonly IGuestyOpenApiClient _client;

        public GuestyScheduleService(IGuestyIntegrationService integration, IGuestyOpenApiClient client)
        {
            _integration = integration;
            _client = client;
        }

        public async Task<GuestyScheduleResponse> GetScheduleAsync(
            string startDate,
            string endDate,
            IEnumerable<string>? listingIds = null,
            int listingsLimit = 25,
            string? city = null,
            string? status = null)
        {
            if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
                throw new BadRequestException("startDate e endDate são obrigatórios (YYYY-MM-DD).");

            var token = await _integration.GetAccessTokenOrThrowAsync();

            // 1) Listings
            List<GuestyListingDTO> listings;
            var listingIdList = listingIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();

            if (listingIdList == null || listingIdList.Count == 0)
            {
                listings = await _client.GetListingsAsync(token, listingsLimit, null, city, status);
                listingIdList = listings.Where(l => !string.IsNullOrWhiteSpace(l.Id)).Select(l => l.Id).Distinct().ToList();
            }
            else
            {
                // We still fetch listing metadata so the UI has the left column filled (best-effort).
                listings = await _client.GetListingsAsync(token, listingsLimit, null, city, status);
                listings = listings.Where(l => listingIdList.Contains(l.Id)).ToList();
            }

            // 2) Calendar raw (bulk)
            var raw = await _client.GetCalendarRawAsync(token, startDate, endDate, listingIdList);

            // 3) Normalize blocks to events
            var events = NormalizeCalendarEvents(raw);

            return new GuestyScheduleResponse
            {
                StartDate = startDate,
                EndDate = endDate,
                Listings = listings,
                Events = events
            };
        }

        private static List<GuestyScheduleEventDTO> NormalizeCalendarEvents(string rawJson)
{
    // rawJson is an aggregated array from GuestyOpenApiClient:
    // [ { listingId: "...", calendar: { days: [...] } }, ... ]
    var events = new List<GuestyScheduleEventDTO>();

    try
    {
        using var doc = JsonDocument.Parse(rawJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return events;

        foreach (var wrapper in doc.RootElement.EnumerateArray())
        {
            var listingId = TryGetString(wrapper, "listingId") ?? "";
            if (string.IsNullOrWhiteSpace(listingId)) continue;

            // Calendar can be an object with `days` array, or directly an array.
            if (!wrapper.TryGetProperty("calendar", out var cal))
                continue;

            JsonElement days;
            if (cal.ValueKind == JsonValueKind.Object && cal.TryGetProperty("days", out days) && days.ValueKind == JsonValueKind.Array)
            {
                // ok
            }
            else if (cal.ValueKind == JsonValueKind.Array)
            {
                days = cal;
            }
            else
            {
                continue;
            }

            // Build "unavailable" blocks by grouping consecutive unavailable days.
            DateTime? blockStart = null;
            DateTime? blockEnd = null;

            void FlushBlock()
            {
                if (!blockStart.HasValue || !blockEnd.HasValue) return;

                var startStr = blockStart.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                // end is exclusive (+1 day)
                var endExclusive = blockEnd.Value.AddDays(1);
                var endStr = endExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                var id = $"unavailable:{listingId}:{startStr}";
                events.Add(new GuestyScheduleEventDTO
                {
                    Id = id,
                    ListingId = listingId,
                    Type = "Block",
                    BlockType = "unavailable",
                    StartDate = startStr,
                    EndDate = endStr,
                    Status = "unavailable",
                    Label = "Unavailable"
                });

                blockStart = null;
                blockEnd = null;
            }

            foreach (var day in days.EnumerateArray())
            {
                var dateStr = TryGetString(day, "date") ?? TryGetString(day, "day") ?? TryGetString(day, "Date");
                if (string.IsNullOrWhiteSpace(dateStr) || !TryParseDate(dateStr, out var date))
                    continue;

                // We treat multiple possible fields as "availability"
                // Common possibilities: available, isAvailable, availableForReservation, isBookable
                var available = TryGetBool(day, "available")
                    ?? TryGetBool(day, "isAvailable")
                    ?? TryGetBool(day, "availableForReservation")
                    ?? TryGetBool(day, "isBookable");

                // If API doesn't provide an availability flag, we can't confidently create blocks.
                if (!available.HasValue)
                    continue;

                if (available.Value == false)
                {
                    if (!blockStart.HasValue)
                    {
                        blockStart = date;
                        blockEnd = date;
                    }
                    else
                    {
                        // if consecutive day
                        if (blockEnd.Value.AddDays(1) == date)
                        {
                            blockEnd = date;
                        }
                        else
                        {
                            FlushBlock();
                            blockStart = date;
                            blockEnd = date;
                        }
                    }
                }
                else
                {
                    FlushBlock();
                }
            }

            FlushBlock();
        }
    }
    catch
    {
        return new List<GuestyScheduleEventDTO>();
    }

    return events
        .OrderBy(e => e.ListingId)
        .ThenBy(e => e.StartDate)
        .ToList();
}

private static IEnumerable<JsonElement> ExtractCalendars(JsonElement root)
{
    // kept for backward compatibility; unused with booking engine aggregation
    if (root.ValueKind == JsonValueKind.Array)
        return root.EnumerateArray();

    if (root.ValueKind == JsonValueKind.Object)
    {
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            return results.EnumerateArray();

        return new[] { root };
    }

    return Array.Empty<JsonElement>();
}

        private static bool TryGetArray(JsonElement el, string prop, out JsonElement arr)
        {
            arr = default;
            if (el.ValueKind != JsonValueKind.Object) return false;
            if (!el.TryGetProperty(prop, out arr)) return false;
            return arr.ValueKind == JsonValueKind.Array;
        }

        private static string? TryGetString(JsonElement? el, string path)
        {
            if (!el.HasValue) return null;
            var current = el.Value;

            foreach (var part in path.Split('.'))
            {
                if (current.ValueKind != JsonValueKind.Object) return null;
                if (!current.TryGetProperty(part, out current)) return null;
            }

            if (current.ValueKind == JsonValueKind.String) return current.GetString();
            if (current.ValueKind == JsonValueKind.Number) return current.ToString();
            if (current.ValueKind == JsonValueKind.True) return "true";
            if (current.ValueKind == JsonValueKind.False) return "false";
            return current.ToString();
        }

        private static bool? TryGetBool(JsonElement el, string path)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;

            var current = el;
            foreach (var part in path.Split('.'))
            {
                if (current.ValueKind != JsonValueKind.Object) return null;
                if (!current.TryGetProperty(part, out current)) return null;
            }

            if (current.ValueKind == JsonValueKind.True) return true;
            if (current.ValueKind == JsonValueKind.False) return false;

            if (current.ValueKind == JsonValueKind.String)
            {
                var s = current.GetString();
                if (bool.TryParse(s, out var b)) return b;
                if (int.TryParse(s, out var i)) return i != 0;
            }

            if (current.ValueKind == JsonValueKind.Number)
            {
                if (current.TryGetInt32(out var i)) return i != 0;
                if (current.TryGetDouble(out var d)) return Math.Abs(d) > 0.0000001;
            }

            return null;
        }

        private static JsonElement? TryGetObject(JsonElement el, string prop)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (!el.TryGetProperty(prop, out var o)) return null;
            if (o.ValueKind != JsonValueKind.Object) return null;
            return o;
        }

        private static string? TryConcatName(JsonElement? guestObj)
        {
            if (!guestObj.HasValue) return null;
            var g = guestObj.Value;
            var first = TryGetString(g, "firstName");
            var last = TryGetString(g, "lastName");
            var name = (first + " " + last).Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static bool TryParseDate(string input, out DateTime date)
        {
            date = default;
            if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                date = dt.Date;
                return true;
            }
            // Sometimes Guesty returns "YYYY-MM-DD"
            if (DateTime.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt2))
            {
                date = dt2.Date;
                return true;
            }
            return false;
        }

        private struct TempEvent
        {
            public string Id;
            public string ListingId;
            public string Type;
            public string? BlockType;
            public DateTime Start;
            public DateTime End;
            public string? Status;
            public string? Label;
            public string? GuestName;
            public string? ConfirmationCode;
            public string? Source;
        }
    }
}
