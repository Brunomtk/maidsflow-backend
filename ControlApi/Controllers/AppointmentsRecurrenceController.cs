using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Appointment;
using Core.Models;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentsRecurrenceController : ControllerBase
    {
        private readonly DbContextClass _db;

        public AppointmentsRecurrenceController(DbContextClass db)
        {
            _db = db;
        }

        // CREATE (single or recurring)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentDTO dto)
        {
            var tz = ResolveTimeZone(dto.TimeZoneId);

            // Work only with local times (no UTC conversion)
            var startLocal = dto.Start;
            var endLocal   = dto.End;

            // Lista de profissionais (para suportar múltiplos profissionais no mesmo compromisso)
            var professionalIds = (dto.ProfessionalIds != null && dto.ProfessionalIds.Any())
                ? dto.ProfessionalIds.Distinct().ToList()
                : new List<int>();

            // Single (non-recurring) appointment
            if (!dto.IsRecurring)
            {
                // Se vier lista de profissionais, criamos um Appointment por profissional.
                if (professionalIds.Count > 0)
                {
                    var nonRecurringAppointments = new List<Appointment>();

                    foreach (var professionalId in professionalIds)
                    {
                        var appt = MapAppointment(dto, startLocal, endLocal, tz, false, null, professionalId);
                        await _db.Set<Appointment>().AddAsync(appt);
                        nonRecurringAppointments.Add(appt);
                    }

                    await _db.SaveChangesAsync();

                    var firstNonRecurring = nonRecurringAppointments
                        .OrderBy(a => a.Start)
                        .FirstOrDefault();

                    return Ok(firstNonRecurring ?? nonRecurringAppointments.FirstOrDefault());
                }
                else
                {
                    // Comportamento antigo: sem lista => usa ProfessionalId único (ou null)
                    var appt = MapAppointment(dto, startLocal, endLocal, tz, false, null, null);
                    await _db.Set<Appointment>().AddAsync(appt);
                    await _db.SaveChangesAsync();
                    return Ok(appt);
                }
            }

            // Recurring appointment
            if (string.IsNullOrWhiteSpace(dto.RecurrenceRule))
                return BadRequest("RecurrenceRule is required when IsRecurring=true.");

            if (dto.OccurrenceCount is null && dto.RecurrenceEnd is null)
                return BadRequest("Provide either RecurrenceEnd or OccurrenceCount.");

            if (dto.OccurrenceCount is int c && c <= 0)
                return BadRequest("OccurrenceCount must be > 0.");

            if (dto.RecurrenceEnd is DateTime until && until < dto.Start)
                return BadRequest("RecurrenceEnd must be >= Start.");

            var seriesId = Guid.NewGuid();
            var occurrences = ExpandOccurrences(
                dto.RecurrenceRule!,
                startLocal,
                endLocal,
                dto.RecurrenceEnd,
                dto.OccurrenceCount,
                tz
            );

            var toCreate = new List<Appointment>();

            foreach (var (start, end) in occurrences)
            {
                if (professionalIds.Count > 0)
                {
                    foreach (var professionalId in professionalIds)
                    {
                        bool exists = await _db.Set<Appointment>()
                            .AnyAsync(a =>
                                a.SeriesId == seriesId &&
                                a.Start == start &&
                                a.ProfessionalId == professionalId);

                        if (!exists)
                        {
                            var appt = MapAppointment(dto, start, end, tz, true, seriesId, professionalId);
                            toCreate.Add(appt);
                        }
                    }
                }
                else
                {
                    bool exists = await _db.Set<Appointment>()
                        .AnyAsync(a => a.SeriesId == seriesId && a.Start == start);

                    if (!exists)
                    {
                        var appt = MapAppointment(dto, start, end, tz, true, seriesId, null);
                        toCreate.Add(appt);
                    }
                }
            }

            await _db.Set<Appointment>().AddRangeAsync(toCreate);
            await _db.SaveChangesAsync();

            var first = toCreate.OrderBy(a => a.Start).FirstOrDefault();
            return Ok(first ?? toCreate.FirstOrDefault());
        }


        // UPDATE with scope
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDTO dto)
        {
            var current = await _db.Set<Appointment>().FindAsync(id);
            if (current == null) return NotFound();

            var tz = ResolveTimeZone(dto.TimeZoneId ?? current.TimeZoneId);

            if (!current.IsRecurring || current.SeriesId == null || dto.Scope == RecurrenceScope.This)
            {
                await UpdateThisAsync(current, dto, tz);
                await _db.SaveChangesAsync();
                return Ok(current);
            }

            if (dto.Scope == RecurrenceScope.ThisAndFollowing)
            {
                await UpdateThisAndFollowingAsync(current, dto, tz);
                await _db.SaveChangesAsync();
                return Ok(current);
            }

            if (dto.Scope == RecurrenceScope.All)
            {
                await UpdateAllAsync(current, dto, tz);
                await _db.SaveChangesAsync();
                return Ok(current);
            }

            return BadRequest("Invalid scope.");
        }

        // DELETE with scope
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] RecurrenceScope scope = RecurrenceScope.This)
        {
            var current = await _db.Set<Appointment>().FindAsync(id);
            if (current == null) return NotFound();

            if (!current.IsRecurring || current.SeriesId == null || scope == RecurrenceScope.This)
            {
                _db.Set<Appointment>().Remove(current);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            IQueryable<Appointment> q = _db.Set<Appointment>().Where(a => a.SeriesId == current.SeriesId);
            if (scope == RecurrenceScope.ThisAndFollowing)
                q = q.Where(a => a.Start >= current.Start);

            _db.Set<Appointment>().RemoveRange(await q.ToListAsync());
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // GET series
        [HttpGet("series/{seriesId:guid}")]
        public async Task<IActionResult> GetSeries(Guid seriesId)
        {
            var list = await _db.Set<Appointment>()
                .Where(a => a.SeriesId == seriesId)
                .OrderBy(a => a.Start)
                .ToListAsync();
            return Ok(list);
        }

        // ---------------- helpers ----------------

        private static TimeZoneInfo ResolveTimeZone(string? tz)
        {
            if (string.IsNullOrWhiteSpace(tz)) return TimeZoneInfo.Utc;
            try { return TimeZoneInfo.FindSystemTimeZoneById(tz); }
            catch { return TimeZoneInfo.Utc; }
        }

        private static DateTime ToUtc(DateTime local, TimeZoneInfo tz)
            => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);

        private static DateTime FromUtc(DateTime utc, TimeZoneInfo tz)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

        private Appointment MapAppointment(
            CreateAppointmentDTO dto, DateTime start, DateTime end, TimeZoneInfo tz, bool isRecurring, Guid? seriesId, int? professionalId = null)
        {
            return new Appointment
            {
                Title = dto.Title,
                Address = dto.Address,
                Notes = dto.Notes,
                Start = start,
                End = end,
                TimeZoneId = tz.Id,
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                TeamId = dto.TeamId,
                ProfessionalId = professionalId ?? dto.ProfessionalId,
                Status = dto.Status ?? Core.Enums.Appointment.AppointmentStatus.Scheduled,
                Type   = dto.Type   ?? Core.Enums.Appointment.AppointmentType.Regular,
                IsRecurring = isRecurring,
                RecurrenceRule = dto.RecurrenceRule,
                SeriesId = seriesId,
                RecurrenceEnd = dto.RecurrenceEnd,
                OccurrenceCount = dto.OccurrenceCount,
                IsException = false
            };
        }


        // Simple RRULE expansion supporting DAILY and WEEKLY with INTERVAL, BYDAY, COUNT, UNTIL
        private List<(DateTime start, DateTime end)> ExpandOccurrences(
            string rrule, DateTime startLocal, DateTime endLocal, DateTime? endLocalSeries, int? count, TimeZoneInfo tz)
        {
            var rule = ParseRRule(rrule);
            var list = new List<(DateTime, DateTime)>();

            var duration = endLocal - startLocal;
            var occurrences = 0;

            DateTime cursor = startLocal;

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
                var days = rule.ByDay; // e.g., ["MO","WE","FR"]
                if (days.Count == 0) days = new List<string> { DayToByDay(cursor.DayOfWeek) };

                DateTime limit = endLocalSeries ?? startLocal.AddYears(2);
                DateTime weekStart = cursor.Date;
                while (weekStart <= limit && (count == null || occurrences < count.Value))
                {
                    foreach (var d in days)
                    {
                        // Next occurrence for this BYDAY in the current week
                        DateTime dayDate = NextOnOrAfter(weekStart, d);
                        if (dayDate < startLocal.Date) continue;
                        if (dayDate > limit) break;

                        var startCandidate = dayDate.Date
                            .AddHours(startLocal.Hour)
                            .AddMinutes(startLocal.Minute)
                            .AddSeconds(startLocal.Second);

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
            else
            {
                // Fallback: single occurrence
                list.Add((startLocal, endLocal));
            }

            // UNTIL cap (local)
            if (endLocalSeries.HasValue)
            {
                list = list.Where(o => o.Item1 <= endLocalSeries.Value).ToList();
            }

            return list;
        }


        private static string DayToByDay(DayOfWeek dow)
        {
            return dow switch
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
        }

        private static DateTime NextOnOrAfter(DateTime weekStart, string byday)
        {
            var target = byday.ToUpperInvariant();
            var map = new Dictionary<string, DayOfWeek> {
                ["MO"] = DayOfWeek.Monday,
                ["TU"] = DayOfWeek.Tuesday,
                ["WE"] = DayOfWeek.Wednesday,
                ["TH"] = DayOfWeek.Thursday,
                ["FR"] = DayOfWeek.Friday,
                ["SA"] = DayOfWeek.Saturday,
                ["SU"] = DayOfWeek.Sunday,
            };
            var targetDow = map.ContainsKey(target) ? map[target] : DayOfWeek.Monday;

            int diff = (int)targetDow - (int)weekStart.DayOfWeek;
            if (diff < 0) diff += 7;
            return weekStart.AddDays(diff);
        }

        private class RRule
        {
            public string Freq { get; set; } = "DAILY";
            public int Interval { get; set; } = 1;
            public List<string> ByDay { get; set; } = new List<string>();
        }

        private static RRule ParseRRule(string rrule)
        {
            var r = new RRule();
            var parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].ToUpperInvariant();
                var val = kv[1].ToUpperInvariant();

                if (key == "FREQ") r.Freq = val;
                else if (key == "INTERVAL" && int.TryParse(val, out var iv)) r.Interval = Math.Max(1, iv);
                else if (key == "BYDAY") r.ByDay = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                // COUNT and UNTIL handled via parameters (dto.OccurrenceCount / dto.RecurrenceEnd)
            }
            return r;
        }

        private async Task UpdateThisAsync(Appointment current, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            if (!current.IsException)
            {
                current.IsException = true;
                current.OriginalStart = current.Start;
                current.OriginalEnd   = current.End;
            }

            if (dto.Title != null) current.Title = dto.Title;
            if (dto.Address != null) current.Address = dto.Address;
            if (dto.Notes != null) current.Notes = dto.Notes;
            if (dto.TimeZoneId != null) current.TimeZoneId = tz.Id;
            if (dto.Start.HasValue && dto.End.HasValue)
            {
                current.Start = dto.Start.Value;
                current.End   = dto.End.Value;
            }
        }


        private async Task UpdateThisAndFollowingAsync(Appointment anchor, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            var seriesId = anchor.SeriesId;
            if (seriesId == null)
            {
                await UpdateThisAsync(anchor, dto, tz);
                return;
            }

            var following = await _db.Set<Appointment>()
                .Where(a => a.SeriesId == seriesId && a.Start >= anchor.Start && !a.IsException)
                .OrderBy(a => a.Start)
                .ToListAsync();

            _db.Set<Appointment>().RemoveRange(following);

            var baseStartLocal = dto.Start ?? anchor.Start;
            var baseEndLocal   = dto.End   ?? anchor.End;
            var rrule = dto.RecurrenceRule ?? anchor.RecurrenceRule ?? "FREQ=DAILY;INTERVAL=1";
            var untilLocal = dto.RecurrenceEnd ?? anchor.RecurrenceEnd;
            var count = dto.OccurrenceCount ?? anchor.OccurrenceCount;

            var newSeriesId = Guid.NewGuid();
            var occurrences = ExpandOccurrences(rrule, baseStartLocal, baseEndLocal, untilLocal, count, tz);
            var toCreate = new List<Appointment>();
            foreach (var (start, end) in occurrences)
            {
                if (start < anchor.Start) continue;
                toCreate.Add(new Appointment
                {
                    Title = dto.Title ?? anchor.Title,
                    Address = dto.Address ?? anchor.Address,
                    Notes = dto.Notes ?? anchor.Notes,
                    Start = start,
                    End = end,
                    TimeZoneId = tz.Id,
                    CompanyId = anchor.CompanyId,
                    CustomerId = anchor.CustomerId,
                    TeamId = anchor.TeamId,
                    ProfessionalId = anchor.ProfessionalId,
                    Status = anchor.Status,
                    Type = anchor.Type,
                    IsRecurring = true,
                    RecurrenceRule = rrule,
                    SeriesId = newSeriesId,
                    RecurrenceEnd = untilLocal ?? anchor.RecurrenceEnd,
                    OccurrenceCount = count,
                    IsException = false
                });
            }

            await _db.Set<Appointment>().AddRangeAsync(toCreate);
        }


        private async Task UpdateAllAsync(Appointment anchor, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            if (anchor.SeriesId == null)
            {
                await UpdateThisAsync(anchor, dto, tz);
                return;
            }

            var all = await _db.Set<Appointment>().Where(a => a.SeriesId == anchor.SeriesId).ToListAsync();
            foreach (var a in all)
            {
                if (a.IsException) continue;
                if (dto.Title != null) a.Title = dto.Title;
                if (dto.Address != null) a.Address = dto.Address;
                if (dto.Notes != null) a.Notes = dto.Notes;
                if (dto.TimeZoneId != null) a.TimeZoneId = tz.Id;
                if (dto.Start.HasValue && dto.End.HasValue)
                {
                    a.Start = dto.Start.Value;
                    a.End   = dto.End.Value;
                }
                if (dto.IsRecurring.HasValue) a.IsRecurring = dto.IsRecurring.Value;
                if (dto.RecurrenceRule != null) a.RecurrenceRule = dto.RecurrenceRule;
                if (dto.RecurrenceEnd.HasValue) a.RecurrenceEnd = dto.RecurrenceEnd.Value;
                if (dto.OccurrenceCount.HasValue) a.OccurrenceCount = dto.OccurrenceCount.Value;
            }
        }

    }
}