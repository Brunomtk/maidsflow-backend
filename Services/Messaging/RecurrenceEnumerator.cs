using Core.Models;

namespace Services.Messaging
{
    /// <summary>
    /// Lightweight RRULE expansion for the messaging hosted services.
    /// Supports the same subset as ReportsService.ExpandOccurrences (FREQ + INTERVAL + BYDAY + BYMONTHDAY)
    /// without the full library overhead, so we can find occurrences inside a sending window.
    /// </summary>
    public static class RecurrenceEnumerator
    {
        public record Occurrence(DateTime Start, DateTime End);

        /// <summary>
        /// Enumerate all occurrences of the given recurring appointment that START inside [windowStart, windowEnd].
        /// Returns at most <paramref name="maxOccurrences"/> matches.
        ///
        /// Honors:
        ///   - FREQ=DAILY|WEEKLY|MONTHLY
        ///   - INTERVAL=N (default 1)
        ///   - BYDAY=MO,TU,...
        ///   - RecurrenceEnd  (stops generating after this date)
        ///   - OccurrenceCount (caps total occurrences from anchor.Start)
        ///
        /// Does NOT honor exceptions (cancellations / overrides) — caller must filter those after.
        /// </summary>
        public static IEnumerable<Occurrence> ExpandInWindow(
            Appointment anchor,
            DateTime windowStart,
            DateTime windowEnd,
            int maxOccurrences = 200)
        {
            if (!anchor.IsRecurring || string.IsNullOrWhiteSpace(anchor.RecurrenceRule))
                yield break;

            var rule = ParseRRule(anchor.RecurrenceRule);
            var duration = anchor.End - anchor.Start;
            var anchorStart = anchor.Start;

            DateTime hardStop = anchor.RecurrenceEnd ?? windowEnd.AddDays(1);
            int countCap = anchor.OccurrenceCount.HasValue && anchor.OccurrenceCount.Value > 0
                ? anchor.OccurrenceCount.Value : int.MaxValue;

            int generated = 0;
            int yielded = 0;

            switch (rule.Freq)
            {
                case "DAILY":
                {
                    var cursor = anchorStart;
                    while (cursor <= hardStop && cursor <= windowEnd)
                    {
                        if (++generated > countCap) yield break;
                        if (cursor >= windowStart)
                        {
                            yield return new Occurrence(cursor, cursor + duration);
                            if (++yielded >= maxOccurrences) yield break;
                        }
                        cursor = cursor.AddDays(rule.Interval);
                    }
                    break;
                }
                case "WEEKLY":
                {
                    var byDay = rule.ByDay.Count > 0 ? rule.ByDay : new List<string> { DayToByDay(anchorStart.DayOfWeek) };
                    // Walk week by week (jump INTERVAL weeks each time), emitting all matching weekdays inside.
                    var weekStart = StartOfWeek(anchorStart.Date); // Monday-based for simplicity
                    while (weekStart <= hardStop && weekStart <= windowEnd.AddDays(7))
                    {
                        foreach (var bd in byDay)
                        {
                            var date = NextOnOrAfter(weekStart, bd);
                            var occ = new DateTime(date.Year, date.Month, date.Day, anchorStart.Hour, anchorStart.Minute, anchorStart.Second, anchorStart.Kind);
                            if (occ < anchorStart) continue;
                            if (occ > hardStop) continue;
                            if (++generated > countCap) yield break;
                            if (occ >= windowStart && occ <= windowEnd)
                            {
                                yield return new Occurrence(occ, occ + duration);
                                if (++yielded >= maxOccurrences) yield break;
                            }
                        }
                        weekStart = weekStart.AddDays(7 * rule.Interval);
                    }
                    break;
                }
                case "MONTHLY":
                {
                    var byMonthDay = rule.ByMonthDay.Count > 0 ? rule.ByMonthDay : new List<int> { anchorStart.Day };
                    var monthCursor = new DateTime(anchorStart.Year, anchorStart.Month, 1, 0, 0, 0, anchorStart.Kind);
                    while (monthCursor <= hardStop && monthCursor <= windowEnd.AddMonths(1))
                    {
                        foreach (var d in byMonthDay)
                        {
                            int safeDay = Math.Min(d, DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month));
                            var occ = new DateTime(monthCursor.Year, monthCursor.Month, safeDay,
                                anchorStart.Hour, anchorStart.Minute, anchorStart.Second, anchorStart.Kind);
                            if (occ < anchorStart) continue;
                            if (occ > hardStop) continue;
                            if (++generated > countCap) yield break;
                            if (occ >= windowStart && occ <= windowEnd)
                            {
                                yield return new Occurrence(occ, occ + duration);
                                if (++yielded >= maxOccurrences) yield break;
                            }
                        }
                        monthCursor = monthCursor.AddMonths(rule.Interval);
                    }
                    break;
                }
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

        private static DateTime StartOfWeek(DateTime d)
        {
            var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return d.AddDays(-diff);
        }

        private static DateTime NextOnOrAfter(DateTime weekStart, string byDay)
        {
            var target = byDay switch
            {
                "MO" => DayOfWeek.Monday, "TU" => DayOfWeek.Tuesday, "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday, "FR" => DayOfWeek.Friday, "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday, _ => weekStart.DayOfWeek,
            };
            var date = weekStart.Date;
            while (date.DayOfWeek != target) date = date.AddDays(1);
            return date;
        }
    }
}
