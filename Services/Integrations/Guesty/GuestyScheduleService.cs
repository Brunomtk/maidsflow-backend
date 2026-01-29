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
            int listingsLimit = 100,
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

                    // Parse and sort days
                    var parsedDays = new List<(DateTime date, JsonElement day)>();
                    foreach (var day in days.EnumerateArray())
                    {
                        var dateStr = TryGetString(day, "date") ?? TryGetString(day, "day") ?? TryGetString(day, "Date");
                        if (string.IsNullOrWhiteSpace(dateStr) || !TryParseDate(dateStr, out var date))
                            continue;

                        parsedDays.Add((date, day));
                    }

                    parsedDays = parsedDays.OrderBy(d => d.date).ToList();
                    if (parsedDays.Count == 0) continue;

                    // Unavailable block accumulator
                    DateTime? unStart = null;
                    DateTime? unEnd = null;

                    // Reservation accumulator
                    string? resKey = null;
                    string? resGuest = null;
                    string? resConf = null;
                    string? resStatus = null;
                    DateTime? resStart = null;
                    DateTime? resEnd = null;

                    void FlushUnavailable()
                    {
                        if (!unStart.HasValue || !unEnd.HasValue) return;

                        var startStr = unStart.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var endExclusive = unEnd.Value.AddDays(1);
                        var endStr = endExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                        events.Add(new GuestyScheduleEventDTO
                        {
                            Id = $"unavailable:{listingId}:{startStr}",
                            ListingId = listingId,
                            Type = "Block",
                            BlockType = "unavailable",
                            StartDate = startStr,
                            EndDate = endStr,
                            Status = "unavailable",
                            Label = "Unavailable"
                        });

                        unStart = null;
                        unEnd = null;
                    }

                    void FlushReservation()
                    {
                        if (!resStart.HasValue || !resEnd.HasValue) return;

                        var startStr = resStart.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var endExclusive = resEnd.Value.AddDays(1);
                        var endStr = endExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                        var label = !string.IsNullOrWhiteSpace(resGuest)
                            ? resGuest
                            : (!string.IsNullOrWhiteSpace(resConf) ? $"Reservation {resConf}" : "Reservation");

                        var idKey = !string.IsNullOrWhiteSpace(resKey) ? resKey : startStr;

                        events.Add(new GuestyScheduleEventDTO
                        {
                            Id = $"reservation:{listingId}:{idKey}:{startStr}",
                            ListingId = listingId,
                            Type = "Reservation",
                            BlockType = "reservation",
                            StartDate = startStr,
                            EndDate = endStr,
                            Status = resStatus ?? "reserved",
                            Label = label,
                            GuestName = resGuest,
                            ConfirmationCode = resConf,
                            Source = "guesty"
                        });

                        resKey = null;
                        resGuest = null;
                        resConf = null;
                        resStatus = null;
                        resStart = null;
                        resEnd = null;
                    }

                    DateTime? prevDate = null;

                    foreach (var (date, day) in parsedDays)
                    {
                        // availability flags (multiple possible fields)
                        var available = TryGetBool(day, "available")
                            ?? TryGetBool(day, "isAvailable")
                            ?? TryGetBool(day, "availableForReservation")
                            ?? TryGetBool(day, "isBookable");

                        // reservation-ish flags/fields (best-effort)
                        var reservationId = FindFirstString(day, "reservationId", "reservation_id", "reservation._id", "reservation.id", "reservationId._id", "reservation");
                        var confirmation = FindFirstString(day, "confirmationCode", "confirmation_code", "reservation.confirmationCode", "reservation.confirmation_code");
                        var statusStr = FindFirstString(day, "status", "bookingStatus", "reservation.status", "reservation.bookingStatus");
                        var isReserved = FindFirstBool(day, "isReserved", "reserved", "booked", "isBooked");

                        var guestName =
                            FindFirstString(day, "guest.fullName", "guest.name", "reservation.guest.fullName", "reservation.guest.name", "guestName", "guest_name")
                            ?? TryConcatName(TryGetObject(day, "guest"))
                            ?? TryConcatName(TryGetObject(day, "reservation.guest"));

                        // Determine reservation: if any reservation identifiers exist OR status looks booked OR reserved flags.
                        var statusLooksBooked = !string.IsNullOrWhiteSpace(statusStr) &&
                                                new[] { "reserved", "booked", "confirmed", "inquiry" }
                                                    .Contains(statusStr.Trim().ToLowerInvariant());

                        var isReservation = !string.IsNullOrWhiteSpace(reservationId)
                                            || !string.IsNullOrWhiteSpace(confirmation)
                                            || (isReserved.HasValue && isReserved.Value)
                                            || statusLooksBooked;

                        // Determine unavailable block: available==false and not reservation
                        var isUnavailable = available.HasValue && available.Value == false && !isReservation;

                        // If day is free/available, flush both accumulators.
                        if (!isReservation && !isUnavailable)
                        {
                            FlushReservation();
                            FlushUnavailable();
                            prevDate = date;
                            continue;
                        }

                        // Reservations
                        if (isReservation)
                        {
                            FlushUnavailable();

                            var key = reservationId ?? confirmation ?? guestName ?? (statusStr ?? "reservation");
                            var consecutive = prevDate.HasValue && prevDate.Value.AddDays(1) == date;
                            if (!resStart.HasValue)
                            {
                                resKey = key;
                                resGuest = guestName;
                                resConf = confirmation;
                                resStatus = statusStr;
                                resStart = date;
                                resEnd = date;
                            }
                            else if (consecutive && string.Equals(resKey, key, StringComparison.OrdinalIgnoreCase))
                            {
                                resEnd = date;
                                // keep first non-empty values
                                resGuest ??= guestName;
                                resConf ??= confirmation;
                                resStatus ??= statusStr;
                            }
                            else
                            {
                                FlushReservation();
                                resKey = key;
                                resGuest = guestName;
                                resConf = confirmation;
                                resStatus = statusStr;
                                resStart = date;
                                resEnd = date;
                            }

                            prevDate = date;
                            continue;
                        }

                        // Unavailable blocks
                        if (isUnavailable)
                        {
                            FlushReservation();

                            if (!unStart.HasValue)
                            {
                                unStart = date;
                                unEnd = date;
                            }
                            else
                            {
                                // if consecutive day
                                if (unEnd.Value.AddDays(1) == date)
                                {
                                    unEnd = date;
                                }
                                else
                                {
                                    FlushUnavailable();
                                    unStart = date;
                                    unEnd = date;
                                }
                            }

                            prevDate = date;
                            continue;
                        }
                    }

                    FlushReservation();
                    FlushUnavailable();
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

        private static string? FindFirstString(JsonElement el, params string[] keys)
        {
            foreach (var key in keys)
            {
                var found = FindByPathCaseInsensitive(el, key);
                if (found.HasValue)
                {
                    var v = found.Value;
                    if (v.ValueKind == JsonValueKind.String)
                    {
                        var s = v.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                    if (v.ValueKind == JsonValueKind.Number) return v.ToString();
                }
            }
            return null;
        }

        private static bool? FindFirstBool(JsonElement el, params string[] keys)
        {
            foreach (var key in keys)
            {
                var found = FindByPathCaseInsensitive(el, key);
                if (found.HasValue)
                {
                    var v = found.Value;
                    if (v.ValueKind == JsonValueKind.True) return true;
                    if (v.ValueKind == JsonValueKind.False) return false;
                    if (v.ValueKind == JsonValueKind.String)
                    {
                        var s = v.GetString();
                        if (bool.TryParse(s, out var b)) return b;
                        if (int.TryParse(s, out var i)) return i != 0;
                    }
                    if (v.ValueKind == JsonValueKind.Number)
                    {
                        if (v.TryGetInt32(out var i)) return i != 0;
                    }
                }
            }
            return null;
        }

        private static JsonElement? FindByPathCaseInsensitive(JsonElement root, string path)
        {
            // Supports dot paths; also tries case-insensitive property match for each segment.
            var current = root;
            foreach (var rawPart in path.Split('.'))
            {
                if (current.ValueKind != JsonValueKind.Object) return null;

                JsonElement next = default;
                var found = false;

                // direct match first
                if (current.TryGetProperty(rawPart, out next))
                {
                    found = true;
                }
                else
                {
                    foreach (var prop in current.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, rawPart, StringComparison.OrdinalIgnoreCase))
                        {
                            next = prop.Value;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found) return null;
                current = next;
            }

            return current;
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
