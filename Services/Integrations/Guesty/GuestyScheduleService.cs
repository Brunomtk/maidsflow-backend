using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.Guesty;
using Core.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Services.Security;

namespace Services.Integrations.Guesty
{
    public class GuestyScheduleService : IGuestyScheduleService
    {
        private readonly IGuestyIntegrationService _integration;
        private readonly IGuestyOpenApiClient _client;
        private readonly IMemoryCache _cache;
        private readonly ICurrentUser _currentUser;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();

        public GuestyScheduleService(
            IGuestyIntegrationService integration,
            IGuestyOpenApiClient client,
            IMemoryCache cache,
            ICurrentUser currentUser)
        {
            _integration = integration;
            _client = client;
            _cache = cache;
            _currentUser = currentUser;
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

            // Cache strategy (fast + resilient):
            // - Fresh TTL: 5 minutes
            // - Stale window: 2 hours (serve stale while a refresh happens in background)
            var companyId = _currentUser.CompanyId;
            var listKey = NormalizeListKey(listingIds);
            var cacheKey = companyId.HasValue
                ? $"guesty:schedule:v2:{companyId.Value}:{startDate}:{endDate}:{listKey}:{Math.Clamp(listingsLimit, 1, 100)}:{city ?? ""}:{status ?? ""}"
                : null;

            if (cacheKey != null && _cache.TryGetValue(cacheKey, out CacheEnvelope cachedEnv))
            {
                var age = DateTime.UtcNow - cachedEnv.FetchedAtUtc;
                if (age <= TimeSpan.FromMinutes(5))
                    return cachedEnv.Response;

                if (age <= TimeSpan.FromHours(2))
                {
                    _ = RefreshInBackgroundAsync(cacheKey, startDate, endDate, listingIds, listingsLimit, city, status);
                    return cachedEnv.Response;
                }
            }

            var response = await ComputeScheduleUncachedAsync(startDate, endDate, listingIds, listingsLimit, city, status);

            if (cacheKey != null)
            {
                _cache.Set(cacheKey, new CacheEnvelope(response, DateTime.UtcNow), new MemoryCacheEntryOptions
                {
                    // keep a larger absolute TTL so the stale window can be served
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
                });
            }

            return response;
        }

        public async Task WarmupAsync(int days = 30)
        {
            var safeDays = Math.Clamp(days, 1, 90);
            var start = DateTime.UtcNow.Date;
            var end = start.AddDays(safeDays);
            var startStr = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var endStr = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // This will populate the cache.
            await GetScheduleAsync(startStr, endStr, null, 100, null, null);
        }

        private async Task RefreshInBackgroundAsync(
            string cacheKey,
            string startDate,
            string endDate,
            IEnumerable<string>? listingIds,
            int listingsLimit,
            string? city,
            string? status)
        {
            var sem = _refreshLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

            // Non-blocking: if another refresh is already running, bail.
            if (!await sem.WaitAsync(0)) return;

            try
            {
                var fresh = await ComputeScheduleUncachedAsync(startDate, endDate, listingIds, listingsLimit, city, status);
                _cache.Set(cacheKey, new CacheEnvelope(fresh, DateTime.UtcNow), new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
                });
            }
            catch
            {
                // best-effort: keep serving stale
            }
            finally
            {
                sem.Release();
            }
        }

        private async Task<GuestyScheduleResponse> ComputeScheduleUncachedAsync(
            string startDate,
            string endDate,
            IEnumerable<string>? listingIds,
            int listingsLimit,
            string? city,
            string? status)
        {
            var token = await _integration.GetAccessTokenOrThrowAsync();

            async Task<List<GuestyListingDTO>> GetListingsCachedAsync()
            {
                var companyId = _currentUser.CompanyId;
                var safeLimit = Math.Clamp(listingsLimit, 1, 100);
                var key = companyId.HasValue
                    ? $"guesty:listings:v2:{companyId.Value}:{safeLimit}:{city ?? ""}:{status ?? ""}"
                    : null;

                if (key != null && _cache.TryGetValue(key, out List<GuestyListingDTO> cached))
                    return cached;

                var fresh = await _client.GetListingsAsync(token, safeLimit, null, city, status);

                if (key != null)
                {
                    _cache.Set(key, fresh, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                    });
                }

                return fresh;
            }

            // 1) Listings
            List<GuestyListingDTO> listings;
            var listingIdList = listingIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();

            if (listingIdList == null || listingIdList.Count == 0)
            {
                listings = await GetListingsCachedAsync();
                listingIdList = listings.Where(l => !string.IsNullOrWhiteSpace(l.Id)).Select(l => l.Id).Distinct().ToList();
            }
            else
            {
                // Best-effort metadata for left column
                listings = await GetListingsCachedAsync();
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

        private static string NormalizeListKey(IEnumerable<string>? listingIds)
        {
            if (listingIds == null) return "all";

            var list = listingIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x)
                .ToList();

            return list.Count == 0 ? "all" : string.Join(",", list);
        }

        private sealed record CacheEnvelope(GuestyScheduleResponse Response, DateTime FetchedAtUtc);

        
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
