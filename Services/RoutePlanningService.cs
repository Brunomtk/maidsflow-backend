using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.RoutePlanning;
using Core.Enums.Appointment;
using Core.Exceptions;
using Core.Models;
using Infrastructure;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Services.Integrations.GoogleMaps;
using Services.Security;

namespace Services
{
    public interface IRoutePlanningService
    {
        Task<RoutePlanResponseDTO> BuildOptimizedDayRouteAsync(int professionalId, RoutePlanRequestDTO request, CancellationToken ct = default);
    }

    public class RoutePlanningService : IRoutePlanningService
    {
        private readonly IUnitOfWork _uow;
        private readonly DbContextClass _db;
        private readonly IScopeGuard _scope;
        private readonly ICurrentUser _currentUser;
        private readonly IDirectionsService _directions;
        private readonly IGeocodingService _geocoding;

        public RoutePlanningService(
            IUnitOfWork uow,
            DbContextClass db,
            IScopeGuard scope,
            ICurrentUser currentUser,
            IDirectionsService directions,
            IGeocodingService geocoding)
        {
            _uow = uow;
            _db = db;
            _scope = scope;
            _currentUser = currentUser;
            _directions = directions;
            _geocoding = geocoding;
        }

        public async Task<RoutePlanResponseDTO> BuildOptimizedDayRouteAsync(int professionalId, RoutePlanRequestDTO request, CancellationToken ct = default)
        {
            await _scope.EnsureProfessionalAccessAsync(professionalId);

            if (string.IsNullOrWhiteSpace(request.Date))
                throw new BadRequestException("Date is required (yyyy-MM-dd).");

            if (!DateOnly.TryParseExact(request.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new BadRequestException("Invalid Date. Use yyyy-MM-dd.");

            var tzId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "America/Los_Angeles" : request.TimeZoneId.Trim();

            // Appointments are stored as local times. We build local-day boundaries and use OVERLAP (Start < end && End > start).
            var rangeStart = date.ToDateTime(TimeOnly.MinValue);
            var rangeEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var companyId = !_currentUser.IsAdmin ? await _scope.GetScopedCompanyIdAsync() : null;

            // Get occurrences (normal + expanded recurring instances with exceptions) for the day for this professional.
            var occurrences = await GetCalendarOccurrencesForProfessionalAsync(rangeStart, rangeEnd, professionalId, companyId, ct);

            var stopsRaw = new List<(int AppointmentId, string Title, string Address, DateTime Start, DateTime End)>();

            foreach (var o in occurrences.OrderBy(o => o.Start))
            {
                var addr = (o.Address ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(addr) && o.CustomerAddressId.HasValue)
                {
                    var ca = await _uow.CustomerAddresses.GetByIdAsync(o.CustomerAddressId.Value);
                    if (ca != null)
                    {
                        addr = BuildCustomerAddressLine(ca);
                    }
                }

                if (string.IsNullOrWhiteSpace(addr))
                    continue;

                var title = !string.IsNullOrWhiteSpace(o.Title) ? o.Title! : "Appointment";
                var apptId = o.AppointmentId ?? o.AnchorAppointmentId ?? 0;
                if (apptId <= 0) continue;

                stopsRaw.Add((apptId, title, addr, o.Start, o.End));
            }

            if (stopsRaw.Count == 0)
            {
                return new RoutePlanResponseDTO
                {
                    Date = request.Date.Trim(),
                    TimeZoneId = tzId,
                    Origin = request.StartAddress ?? string.Empty,
                    Destination = request.EndAddress ?? string.Empty,
                    Stops = new List<RoutePlanStopDTO>()
                };
            }

            // Default origin/destination from first/last stop if not specified.
            var origin = string.IsNullOrWhiteSpace(request.StartAddress) ? stopsRaw.First().Address : request.StartAddress!.Trim();
            var destination = string.IsNullOrWhiteSpace(request.EndAddress) ? stopsRaw.Last().Address : request.EndAddress!.Trim();

            // Waypoints: if origin/destination not overridden, optimize internal stops only.
            List<(int AppointmentId, string Title, string Address, DateTime Start, DateTime End)> waypointStops;
            if (string.IsNullOrWhiteSpace(request.StartAddress) && string.IsNullOrWhiteSpace(request.EndAddress))
            {
                waypointStops = stopsRaw.Skip(1).Take(Math.Max(0, stopsRaw.Count - 2)).ToList();
            }
            else
            {
                waypointStops = stopsRaw.ToList();
            }

            // Geocode each stop address to ensure we can always render pins and build fallback polylines.
            // (Directions may be unavailable if API isn't enabled; we still return coords for every stop.)
            var geocoded = new List<(int AppointmentId, string Title, string Address, double? Lat, double? Lng, DateTime Start, DateTime End)>();
            foreach (var s in stopsRaw)
            {
                var coords = await _geocoding.GeocodeAsync(s.Address, ct);
                geocoded.Add((s.AppointmentId, s.Title, s.Address, coords?.Latitude, coords?.Longitude, s.Start, s.End));
            }

            // Try to call Directions (if enabled) for true distance/duration and overview polyline.
            double totalKm = 0;
            int totalMinutes = 0;
            string? overviewPolyline = null;

            try
            {
                var waypoints = waypointStops.Select(w => w.Address).ToList();
                var dir = await _directions.GetOptimizedRouteAsync(origin, destination, waypoints, request.Mode ?? "driving", ct);

                if (dir != null)
                {
                    totalKm = dir.TotalDistanceMeters / 1000.0;
                    totalMinutes = (int)Math.Round(dir.TotalDurationSeconds / 60.0);
                    overviewPolyline = dir.OverviewPolyline;

                    // If Directions returned an optimized waypoint order, we should reorder stops accordingly.
                    // We keep origin/destination stops anchored unless custom origin/destination provided.
                    if (dir.WaypointOrder != null && dir.WaypointOrder.Length > 0 && waypointStops.Count == dir.WaypointOrder.Length)
                    {
                        var reordered = dir.WaypointOrder.Select(i => waypointStops[i]).ToList();
                        if (string.IsNullOrWhiteSpace(request.StartAddress) && string.IsNullOrWhiteSpace(request.EndAddress))
                        {
                            var first = stopsRaw.First();
                            var last = stopsRaw.Last();
                            stopsRaw = new List<(int, string, string, DateTime, DateTime)>();
                            stopsRaw.Add(first);
                            stopsRaw.AddRange(reordered);
                            stopsRaw.Add(last);
                        }
                        else
                        {
                            stopsRaw = reordered;
                        }

                        // Rebuild geocoded in the same order.
                        var geoMap = geocoded.ToDictionary(x => (x.AppointmentId, x.Address));
                        geocoded = stopsRaw.Select(s =>
                        {
                            if (geoMap.TryGetValue((s.AppointmentId, s.Address), out var g))
                                return g;
                            var coords = _geocoding.GeocodeAsync(s.Address, ct).GetAwaiter().GetResult();
                            return (s.AppointmentId, s.Title, s.Address, coords?.Latitude, coords?.Longitude, s.Start, s.End);
                        }).ToList();
                    }
                }
            }
            catch
            {
                // Keep fallback (coords only). Totals will remain 0 and polyline null.
            }

            return new RoutePlanResponseDTO
            {
                Date = request.Date.Trim(),
                TimeZoneId = tzId,
                Origin = origin,
                Destination = destination,
                TotalDistanceKm = totalKm,
                TotalDurationMinutes = totalMinutes,
                OverviewPolyline = overviewPolyline,
                Stops = geocoded.Select(s => new RoutePlanStopDTO
                {
                    AppointmentId = s.AppointmentId,
                    Title = s.Title,
                    Address = s.Address,
                    Latitude = s.Lat,
                    Longitude = s.Lng,
                    Start = s.Start,
                    End = s.End
                }).ToList()
            };
        }

        private static string BuildCustomerAddressLine(CustomerAddress addr)
        {
            var parts = new List<string>();
            var line1 = addr.AddressLine1?.Trim();
            var line2 = addr.AddressLine2?.Trim();
            if (!string.IsNullOrWhiteSpace(line1)) parts.Add(line1!);
            if (!string.IsNullOrWhiteSpace(line2)) parts.Add(line2!);

            var cityState = string.Join(", ", new[] { addr.City?.Trim(), addr.State?.Trim() }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(cityState)) parts.Add(cityState);

            var zip = addr.ZipCode?.Trim();
            if (!string.IsNullOrWhiteSpace(zip)) parts.Add(zip!);

            return string.Join(" - ", parts);
        }

        private async Task<List<CalendarRow>> GetCalendarOccurrencesForProfessionalAsync(
            DateTime rangeStart,
            DateTime rangeEnd,
            int professionalId,
            int? companyId,
            CancellationToken ct)
        {
            // Normal appointments (non-recurring) overlapping the range
            var normalQuery = _db.Set<Appointment>().AsNoTracking()
                .Include(a => a.CustomerAddress)
                .Where(a => !a.IsRecurring && a.Start < rangeEnd && a.End > rangeStart)
                .Where(a => a.Status != AppointmentStatus.Cancelled)
                .Where(a => !_db.Set<Cancellation>().Any(c => c.AppointmentId == a.Id));

            if (companyId.HasValue)
                normalQuery = normalQuery.Where(a => a.CompanyId == companyId.Value);

            normalQuery = FilterByProfessional(normalQuery, professionalId);

            var normals = await normalQuery.ToListAsync(ct);

            var outList = new List<CalendarRow>();

            foreach (var a in normals)
            {
                outList.Add(new CalendarRow
                {
                    AppointmentId = a.Id,
                    AnchorAppointmentId = null,
                    Start = a.Start,
                    End = a.End,
                    Title = a.Title ?? a.Customer?.Name ?? "Appointment",
                    Address = ResolveAddressString(a.Address, null, a.CustomerAddress),
                    CustomerAddressId = a.CustomerAddressId
                });
            }

            // Recurring anchors
            var anchorsQuery = _db.Set<Appointment>().AsNoTracking()
                .Include(a => a.Customer)
                .Include(a => a.CustomerAddress)
                .Where(a => a.IsRecurring
                         && a.SeriesId != null
                         && !string.IsNullOrWhiteSpace(a.RecurrenceRule)
                         && a.Start <= rangeEnd
                         && (!a.RecurrenceEnd.HasValue || a.RecurrenceEnd.Value >= rangeStart));

            anchorsQuery = anchorsQuery.Where(a => a.Status != AppointmentStatus.Cancelled)
                                     .Where(a => !_db.Set<Cancellation>().Any(c => c.AppointmentId == a.Id));

            if (companyId.HasValue)
                anchorsQuery = anchorsQuery.Where(a => a.CompanyId == companyId.Value);

            // For anchors, we cannot safely filter by professional in SQL because ProfessionalIds is NotMapped, but we can filter by ProfessionalIdsData.
            anchorsQuery = FilterByProfessional(anchorsQuery, professionalId);

            var anchors = await anchorsQuery.ToListAsync(ct);
            if (anchors.Count == 0) return outList;

            var seriesIds = anchors.Where(a => a.SeriesId.HasValue).Select(a => a.SeriesId!.Value).Distinct().ToList();

            var exceptions = await _db.Set<AppointmentRecurrenceException>().AsNoTracking()
                .Where(e => seriesIds.Contains(e.SeriesId))
                .ToListAsync(ct);

            var exMap = exceptions.ToDictionary(e => (e.SeriesId, e.OccurrenceStart), e => e);

            foreach (var anchor in anchors)
            {
                if (!anchor.SeriesId.HasValue) continue;

                // Limit generation to min(rangeEnd, RecurrenceEnd)
                DateTime? seriesEnd = anchor.RecurrenceEnd.HasValue
                    ? (anchor.RecurrenceEnd.Value < rangeEnd ? anchor.RecurrenceEnd.Value : rangeEnd)
                    : rangeEnd;

                var occs = ExpandOccurrences(
                    anchor.RecurrenceRule!,
                    anchor.Start,
                    anchor.End,
                    seriesEnd,
                    anchor.OccurrenceCount);

                foreach (var (occStart, occEnd) in occs)
                {
                    if (occStart >= rangeEnd || occEnd <= rangeStart) continue;

                    var key = (anchor.SeriesId!.Value, occStart);
                    if (exMap.TryGetValue(key, out var ex))
                    {
                        if (ex.IsCancelled) continue;
                        if (ex.OverrideStatus.HasValue && ex.OverrideStatus.Value == AppointmentStatus.Cancelled) continue;

                        var startFinal = ex.OverrideStart ?? occStart;
                        var endFinal = ex.OverrideEnd ?? occEnd;
                        if (startFinal >= rangeEnd || endFinal <= rangeStart) continue;

                        var finalProfessionalIds = (ex.OverrideProfessionalIds != null && ex.OverrideProfessionalIds.Any())
                            ? ex.OverrideProfessionalIds.Distinct().ToList()
                            : anchor.ProfessionalIds?.Distinct().ToList() ?? new List<int>();

                        if (!finalProfessionalIds.Contains(professionalId))
                            continue;

                        var finalCustomerAddressId = ex.OverrideCustomerAddressId ?? anchor.CustomerAddressId;
                        var resolvedAddress = ResolveAddressString(
                            ex.OverrideAddress,
                            anchor.Address,
                            anchor.CustomerAddress);

                        outList.Add(new CalendarRow
                        {
                            AppointmentId = anchor.Id,
                            AnchorAppointmentId = anchor.Id,
                            Start = startFinal,
                            End = endFinal,
                            Title = !string.IsNullOrWhiteSpace(ex.OverrideTitle) ? ex.OverrideTitle! : (anchor.Title ?? anchor.Customer?.Name ?? "Appointment"),
                            Address = resolvedAddress,
                            CustomerAddressId = finalCustomerAddressId
                        });

                        continue;
                    }

                    // no exception
                    if (anchor.ProfessionalIds == null || !anchor.ProfessionalIds.Contains(professionalId))
                        continue;

                    outList.Add(new CalendarRow
                    {
                        AppointmentId = anchor.Id,
                        AnchorAppointmentId = anchor.Id,
                        Start = occStart,
                        End = occEnd,
                        Title = anchor.Title ?? anchor.Customer?.Name ?? "Appointment",
                        Address = ResolveAddressString(null, anchor.Address, anchor.CustomerAddress),
                        CustomerAddressId = anchor.CustomerAddressId
                    });
                }
            }

            return outList;
        }

        private static IQueryable<Appointment> FilterByProfessional(IQueryable<Appointment> query, int professionalId)
        {
            var idStr = professionalId.ToString();
            var exact = "[" + idStr + "]";
            var atStart = "[" + idStr + ",";
            var atEnd = "," + idStr + "]";
            var middle = "," + idStr + ",";

            return query.Where(a => a.ProfessionalIdsData != null &&
                                    (a.ProfessionalIdsData == exact ||
                                     a.ProfessionalIdsData.StartsWith(atStart) ||
                                     a.ProfessionalIdsData.EndsWith(atEnd) ||
                                     a.ProfessionalIdsData.Contains(middle)));
        }

        private static string ResolveAddressString(string? overrideAddress, string? snapshotAddress, CustomerAddress? navCustomerAddress)
        {
            if (!string.IsNullOrWhiteSpace(overrideAddress)) return overrideAddress!.Trim();
            if (!string.IsNullOrWhiteSpace(snapshotAddress)) return snapshotAddress!.Trim();
            if (navCustomerAddress != null) return BuildCustomerAddressLine(navCustomerAddress);
            return string.Empty;
        }

        // Simple RRULE expansion supporting DAILY, WEEKLY and MONTHLY with INTERVAL, BYDAY, BYMONTHDAY, COUNT, UNTIL.
        private List<(DateTime start, DateTime end)> ExpandOccurrences(
            string rrule,
            DateTime startLocal,
            DateTime endLocal,
            DateTime? endLocalSeries,
            int? count)
        {
            var rule = ParseRRule(rrule);
            var list = new List<(DateTime, DateTime)>();

            var duration = endLocal - startLocal;
            var occurrences = 0;

            DateTime cursor = startLocal;
            var timeOfDay = startLocal.TimeOfDay;

            if (rule.Freq == "DAILY")
            {
                int interval = rule.Interval;
                DateTime limit = endLocalSeries ?? startLocal.AddYears(2);
                while (cursor <= limit && (count == null || occurrences < count.Value))
                {
                    var start = cursor;
                    var end = cursor + duration;
                    list.Add((start, end));
                    occurrences += 1;
                    cursor = cursor.AddDays(interval);
                }
            }
            else if (rule.Freq == "WEEKLY")
            {
                int interval = rule.Interval;
                var days = rule.ByDay;
                if (days.Count == 0) days = new List<string> { DayToByDay(cursor.DayOfWeek) };
                days = days.Select(d => d.ToUpperInvariant()).Distinct().OrderBy(DaySortKey).ToList();

                DateTime limit = endLocalSeries ?? startLocal.AddYears(2);
                DateTime weekStart = cursor.Date;
                while (weekStart <= limit && (count == null || occurrences < count.Value))
                {
                    foreach (var d in days)
                    {
                        DateTime dayDate = NextOnOrAfter(weekStart, d);
                        if (dayDate < startLocal.Date) continue;
                        if (dayDate > limit) break;

                        var startCandidate = dayDate.Date + timeOfDay;
                        if (startCandidate < startLocal) continue;
                        if (endLocalSeries.HasValue && startCandidate > endLocalSeries.Value) continue;
                        if (count != null && occurrences >= count.Value) break;

                        var start = startCandidate;
                        var end = startCandidate + duration;
                        list.Add((start, end));
                        occurrences++;
                    }

                    weekStart = weekStart.AddDays(7 * interval);
                }
            }
            else if (rule.Freq == "MONTHLY")
            {
                int interval = rule.Interval;
                DateTime limit = endLocalSeries ?? startLocal.AddYears(2);

                var monthDays = rule.ByMonthDay;
                int targetDay = monthDays.Count > 0 ? monthDays[0] : startLocal.Day;

                var monthCursor = new DateTime(startLocal.Year, startLocal.Month, 1);

                while (monthCursor <= limit && (count == null || occurrences < count.Value))
                {
                    var year = monthCursor.Year;
                    var month = monthCursor.Month;

                    var daysInMonth = DateTime.DaysInMonth(year, month);
                    if (targetDay >= 1 && targetDay <= daysInMonth)
                    {
                        var dayDate = new DateTime(year, month, targetDay);
                        var startCandidate = dayDate.Date + timeOfDay;

                        if (startCandidate >= startLocal && startCandidate <= limit)
                        {
                            if (!endLocalSeries.HasValue || startCandidate <= endLocalSeries.Value)
                            {
                                var start = startCandidate;
                                var end = startCandidate + duration;
                                list.Add((start, end));
                                occurrences++;
                                if (count != null && occurrences >= count.Value) break;
                            }
                        }
                    }

                    monthCursor = monthCursor.AddMonths(interval);
                }
            }

            return list;
        }

        private class ParsedRRule
        {
            public string Freq { get; set; } = "DAILY";
            public int Interval { get; set; } = 1;
            public List<string> ByDay { get; set; } = new();
            public List<int> ByMonthDay { get; set; } = new();
        }

        private static ParsedRRule ParseRRule(string rrule)
        {
            var rule = new ParsedRRule();
            var parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                var kv = p.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;
                var key = kv[0].ToUpperInvariant();
                var val = kv[1].Trim();

                if (key == "FREQ") rule.Freq = val.ToUpperInvariant();
                else if (key == "INTERVAL" && int.TryParse(val, out var interval)) rule.Interval = Math.Max(1, interval);
                else if (key == "BYDAY")
                {
                    rule.ByDay = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => s.ToUpperInvariant()).ToList();
                }
                else if (key == "BYMONTHDAY")
                {
                    rule.ByMonthDay = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, out var d) ? d : 0)
                        .Where(d => d != 0)
                        .ToList();
                }
            }
            return rule;
        }

        private static int DaySortKey(string byDay)
        {
            return byDay switch
            {
                "MO" => 1,
                "TU" => 2,
                "WE" => 3,
                "TH" => 4,
                "FR" => 5,
                "SA" => 6,
                "SU" => 7,
                _ => 99
            };
        }

        private static string DayToByDay(DayOfWeek d) =>
            d switch
            {
                DayOfWeek.Monday => "MO",
                DayOfWeek.Tuesday => "TU",
                DayOfWeek.Wednesday => "WE",
                DayOfWeek.Thursday => "TH",
                DayOfWeek.Friday => "FR",
                DayOfWeek.Saturday => "SA",
                DayOfWeek.Sunday => "SU",
                _ => "MO"
            };

        private static DateTime NextOnOrAfter(DateTime weekStart, string byDay)
        {
            var target = byDay switch
            {
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday,
                _ => weekStart.DayOfWeek
            };

            var d = weekStart;
            while (d.DayOfWeek != target) d = d.AddDays(1);
            return d;
        }

        private class CalendarRow
        {
            public int? AppointmentId { get; set; }
            public int? AnchorAppointmentId { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string? Title { get; set; }
            public string? Address { get; set; }
            public int? CustomerAddressId { get; set; }
        }
    }
}