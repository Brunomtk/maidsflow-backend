using Core.Models;

namespace Services.Messaging
{
    /// <summary>
    /// RRULE expansion for messaging hosted services. Honors:
    ///   - FREQ=DAILY|WEEKLY|MONTHLY
    ///   - INTERVAL=N (default 1)
    ///   - BYDAY=MO,TU,...  (also positional: 1MO, -1FR for MONTHLY)
    ///   - BYMONTHDAY=1,15
    ///   - RecurrenceEnd / OccurrenceCount caps
    ///
    /// IMPORTANT (TZ): the database stores Appointment.Start as the LOCAL clock-time of the
    /// company's TimeZoneId, with DateTimeKind=Utc on the column. To compare against a UTC
    /// window, we convert each anchor occurrence (local) → UTC via the company tz, then
    /// compare against [windowStartUtc, windowEndUtc]. Yielded `Start`/`End` are real UTC.
    /// </summary>
    public static class RecurrenceEnumerator
    {
        public record Occurrence(DateTime StartUtc, DateTime EndUtc, DateTime OriginalStartLocal);

        /// <summary>
        /// Enumerate occurrences of <paramref name="anchor"/> whose START (in UTC, after tz conversion)
        /// falls inside [windowStartUtc, windowEndUtc].
        /// Honors RecurrenceExceptions: cancellations are skipped; OverrideStart/OverrideEnd reschedule.
        /// </summary>
        public static IEnumerable<Occurrence> ExpandInWindow(
            Appointment anchor,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            TimeZoneInfo tz,
            IReadOnlyList<AppointmentRecurrenceException>? exceptions = null,
            int maxOccurrences = 200)
        {
            if (!anchor.IsRecurring || string.IsNullOrWhiteSpace(anchor.RecurrenceRule))
                yield break;

            var rule = ParseRRule(anchor.RecurrenceRule);
            // anchor.Start is stored as LOCAL clock-time but Kind=Utc — strip to Unspecified for arithmetic
            var anchorLocal = DateTime.SpecifyKind(anchor.Start, DateTimeKind.Unspecified);
            var duration = anchor.End - anchor.Start;

            DateTime hardStopLocal = anchor.RecurrenceEnd.HasValue
                ? DateTime.SpecifyKind(anchor.RecurrenceEnd.Value, DateTimeKind.Unspecified)
                : DateTime.SpecifyKind(windowEndUtc.AddDays(7), DateTimeKind.Unspecified); // generous slack

            int countCap = anchor.OccurrenceCount.HasValue && anchor.OccurrenceCount.Value > 0
                ? anchor.OccurrenceCount.Value : int.MaxValue;

            // Index exceptions by OccurrenceStart (local) for fast lookup
            var exMap = new Dictionary<DateTime, AppointmentRecurrenceException>();
            if (exceptions != null && anchor.SeriesId.HasValue)
            {
                foreach (var e in exceptions)
                {
                    if (e.SeriesId != anchor.SeriesId.Value) continue;
                    var key = DateTime.SpecifyKind(e.OccurrenceStart, DateTimeKind.Unspecified);
                    exMap[key] = e;
                }
            }

            int generated = 0;
            int yielded = 0;

            IEnumerable<DateTime> rawLocalStarts = rule.Freq switch
            {
                "DAILY" => DailyLocalStarts(anchorLocal, hardStopLocal, rule, countCap),
                "WEEKLY" => WeeklyLocalStarts(anchorLocal, hardStopLocal, rule, countCap),
                "MONTHLY" => MonthlyLocalStarts(anchorLocal, hardStopLocal, rule, countCap),
                _ => Enumerable.Empty<DateTime>(),
            };

            foreach (var origLocal in rawLocalStarts)
            {
                if (++generated > countCap) yield break;

                // Apply exception override if present
                DateTime effectiveStartLocal = origLocal;
                DateTime effectiveEndLocal = origLocal + duration;
                if (exMap.TryGetValue(origLocal, out var ex))
                {
                    if (ex.IsCancelled) continue;
                    if (ex.OverrideStart.HasValue)
                        effectiveStartLocal = DateTime.SpecifyKind(ex.OverrideStart.Value, DateTimeKind.Unspecified);
                    if (ex.OverrideEnd.HasValue)
                        effectiveEndLocal = DateTime.SpecifyKind(ex.OverrideEnd.Value, DateTimeKind.Unspecified);
                    else if (ex.OverrideStart.HasValue)
                        effectiveEndLocal = effectiveStartLocal + duration;
                }

                // Convert local → UTC and check window
                var occStartUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(effectiveStartLocal, DateTimeKind.Unspecified), tz);
                var occEndUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(effectiveEndLocal, DateTimeKind.Unspecified), tz);

                if (occStartUtc < windowStartUtc) continue;
                if (occStartUtc > windowEndUtc) continue;

                yield return new Occurrence(occStartUtc, occEndUtc, origLocal);
                if (++yielded >= maxOccurrences) yield break;
            }
        }

        // ----- Local-time generators -----

        private static IEnumerable<DateTime> DailyLocalStarts(DateTime anchorLocal, DateTime hardStopLocal, ParsedRRule rule, int countCap)
        {
            var cursor = anchorLocal;
            int i = 0;
            while (cursor <= hardStopLocal && i++ < countCap)
            {
                yield return cursor;
                cursor = cursor.AddDays(rule.Interval);
            }
        }

        private static IEnumerable<DateTime> WeeklyLocalStarts(DateTime anchorLocal, DateTime hardStopLocal, ParsedRRule rule, int countCap)
        {
            var byDay = rule.ByDay.Count > 0
                ? rule.ByDay.Select(StripPosition).ToList()
                : new List<string> { DayToByDay(anchorLocal.DayOfWeek) };

            var weekStart = StartOfWeek(anchorLocal.Date);
            int i = 0;
            while (weekStart <= hardStopLocal && i < countCap)
            {
                foreach (var bd in byDay)
                {
                    var date = NextOnOrAfter(weekStart, bd);
                    var occ = new DateTime(date.Year, date.Month, date.Day,
                        anchorLocal.Hour, anchorLocal.Minute, anchorLocal.Second, DateTimeKind.Unspecified);
                    if (occ < anchorLocal) continue;
                    if (occ > hardStopLocal) yield break;
                    yield return occ;
                    if (++i >= countCap) yield break;
                }
                weekStart = weekStart.AddDays(7 * rule.Interval);
            }
        }

        private static IEnumerable<DateTime> MonthlyLocalStarts(DateTime anchorLocal, DateTime hardStopLocal, ParsedRRule rule, int countCap)
        {
            var monthCursor = new DateTime(anchorLocal.Year, anchorLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            int emitted = 0;

            while (monthCursor <= hardStopLocal && emitted < countCap)
            {
                // 1) BYMONTHDAY explicit list (1, 15, -1)
                if (rule.ByMonthDay.Count > 0)
                {
                    foreach (var d in rule.ByMonthDay)
                    {
                        int safeDay = ResolveMonthDay(monthCursor.Year, monthCursor.Month, d);
                        if (safeDay <= 0) continue;
                        var occ = new DateTime(monthCursor.Year, monthCursor.Month, safeDay,
                            anchorLocal.Hour, anchorLocal.Minute, anchorLocal.Second, DateTimeKind.Unspecified);
                        if (occ < anchorLocal) continue;
                        if (occ > hardStopLocal) yield break;
                        yield return occ;
                        if (++emitted >= countCap) yield break;
                    }
                }
                // 2) BYDAY with positional prefix (1MO, -1FR) or plain (MO,FR repeated weekly within month)
                else if (rule.ByDay.Count > 0)
                {
                    foreach (var bd in rule.ByDay)
                    {
                        var (pos, dow) = ParsePositionalByDay(bd);
                        var occDate = ResolvePositionalDayInMonth(monthCursor.Year, monthCursor.Month, dow, pos);
                        if (occDate == null) continue;
                        var occ = new DateTime(occDate.Value.Year, occDate.Value.Month, occDate.Value.Day,
                            anchorLocal.Hour, anchorLocal.Minute, anchorLocal.Second, DateTimeKind.Unspecified);
                        if (occ < anchorLocal) continue;
                        if (occ > hardStopLocal) yield break;
                        yield return occ;
                        if (++emitted >= countCap) yield break;
                    }
                }
                // 3) Default: same day-of-month as anchor
                else
                {
                    int safeDay = Math.Min(anchorLocal.Day, DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month));
                    var occ = new DateTime(monthCursor.Year, monthCursor.Month, safeDay,
                        anchorLocal.Hour, anchorLocal.Minute, anchorLocal.Second, DateTimeKind.Unspecified);
                    if (occ >= anchorLocal && occ <= hardStopLocal)
                    {
                        yield return occ;
                        if (++emitted >= countCap) yield break;
                    }
                }
                monthCursor = monthCursor.AddMonths(rule.Interval);
            }
        }

        // ----- Helpers -----

        private sealed class ParsedRRule
        {
            public string Freq { get; set; } = "DAILY";
            public int Interval { get; set; } = 1;
            public List<string> ByDay { get; set; } = new();
            public List<int> ByMonthDay { get; set; } = new();
        }

        private static ParsedRRule ParseRRule(string rrule)
        {
            var rule = new ParsedRRule();
            foreach (var part in rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;
                var key = kv[0].ToUpperInvariant();
                var value = kv[1].Trim();
                if (key == "FREQ") rule.Freq = value.ToUpperInvariant();
                else if (key == "INTERVAL" && int.TryParse(value, out var n)) rule.Interval = Math.Max(1, n);
                else if (key == "BYDAY")
                    rule.ByDay = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => x.ToUpperInvariant()).ToList();
                else if (key == "BYMONTHDAY")
                    rule.ByMonthDay = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => int.TryParse(x, out var d) ? d : 0).Where(x => x != 0).ToList();
            }
            return rule;
        }

        private static string DayToByDay(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => "MO", DayOfWeek.Tuesday => "TU", DayOfWeek.Wednesday => "WE",
            DayOfWeek.Thursday => "TH", DayOfWeek.Friday => "FR", DayOfWeek.Saturday => "SA",
            _ => "SU",
        };

        private static DayOfWeek ByDayToDow(string bd) => bd switch
        {
            "MO" => DayOfWeek.Monday, "TU" => DayOfWeek.Tuesday, "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday, "FR" => DayOfWeek.Friday, "SA" => DayOfWeek.Saturday,
            _ => DayOfWeek.Sunday,
        };

        private static string StripPosition(string bd)
        {
            // "1MO" / "-1FR" → "MO" / "FR"
            int i = 0;
            if (i < bd.Length && bd[i] == '-') i++;
            while (i < bd.Length && char.IsDigit(bd[i])) i++;
            return i == 0 ? bd : bd[i..];
        }

        private static (int Position, DayOfWeek Dow) ParsePositionalByDay(string bd)
        {
            int sign = 1;
            int idx = 0;
            if (idx < bd.Length && bd[idx] == '-') { sign = -1; idx++; }
            int pos = 0;
            while (idx < bd.Length && char.IsDigit(bd[idx])) { pos = pos * 10 + (bd[idx] - '0'); idx++; }
            var dow = ByDayToDow(idx < bd.Length ? bd[idx..] : bd);
            return (sign * pos, dow);
        }

        private static DateTime? ResolvePositionalDayInMonth(int year, int month, DayOfWeek dow, int position)
        {
            int days = DateTime.DaysInMonth(year, month);
            if (position == 0)
            {
                // Plain "MO" with no position → first occurrence in the month is meaningless for monthly;
                // emit the first one inside the month.
                for (int d = 1; d <= days; d++)
                {
                    var dt = new DateTime(year, month, d);
                    if (dt.DayOfWeek == dow) return dt;
                }
                return null;
            }
            if (position > 0)
            {
                int found = 0;
                for (int d = 1; d <= days; d++)
                {
                    var dt = new DateTime(year, month, d);
                    if (dt.DayOfWeek == dow && ++found == position) return dt;
                }
                return null;
            }
            // negative: count from end
            int findFromEnd = -position;
            int seen = 0;
            for (int d = days; d >= 1; d--)
            {
                var dt = new DateTime(year, month, d);
                if (dt.DayOfWeek == dow && ++seen == findFromEnd) return dt;
            }
            return null;
        }

        private static int ResolveMonthDay(int year, int month, int d)
        {
            int days = DateTime.DaysInMonth(year, month);
            if (d > 0) return d > days ? days : d;
            if (d < 0) return Math.Max(1, days + d + 1); // -1 → last day, -2 → second-to-last
            return 0;
        }

        private static DateTime StartOfWeek(DateTime d)
        {
            var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return d.AddDays(-diff);
        }

        private static DateTime NextOnOrAfter(DateTime weekStart, string byDay)
        {
            var target = ByDayToDow(byDay);
            var date = weekStart.Date;
            while (date.DayOfWeek != target) date = date.AddDays(1);
            return date;
        }

        // ----- Convenience used by hosted services -----

        public static TimeZoneInfo ResolveTimeZoneSafe(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Local;
            try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
            catch { return TimeZoneInfo.Local; }
        }

        /// <summary>Treat a naive local DateTime as the company's local clock-time, return real UTC.</summary>
        public static DateTime LocalToUtc(DateTime local, TimeZoneInfo tz)
        {
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        }
    }
}
