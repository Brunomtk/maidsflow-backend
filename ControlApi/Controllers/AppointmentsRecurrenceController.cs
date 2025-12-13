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

            // Sempre trabalhamos com horários locais (sem conversão para UTC aqui).
            var startLocal = dto.Start;
            var endLocal   = dto.End;

            // Agendamento NÃO recorrente: mantém o comportamento original.
            if (!dto.IsRecurring)
            {
                var appointment = MapAppointment(dto, startLocal, endLocal, tz, false, null);
                await _db.Set<Appointment>().AddAsync(appointment);
                await _db.SaveChangesAsync();
                return Ok(appointment);
            }

            // Agendamento recorrente:
            // A partir de agora vamos persistir apenas UM registro na tabela de Appointments,
            // carregando a regra de recorrência (RecurrenceRule / RecurrenceEnd / OccurrenceCount).
            // A expansão em múltiplas ocorrências fica para a camada de leitura.
            if (string.IsNullOrWhiteSpace(dto.RecurrenceRule))
            {
                return BadRequest("RecurrenceRule é obrigatório para agendamentos recorrentes.");
            }

            var seriesId = Guid.NewGuid();
            var recurringAppointment = MapAppointment(dto, startLocal, endLocal, tz, true, seriesId);

            await _db.Set<Appointment>().AddAsync(recurringAppointment);
            await _db.SaveChangesAsync();

            return Ok(recurringAppointment);
        }
// UPDATE with scope
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDTO dto)
        {
            var current = await _db.Set<Appointment>().FindAsync(id);
            if (current == null) return NotFound();

            var tz = ResolveTimeZone(dto.TimeZoneId ?? current.TimeZoneId);

            // Non-recurring (or no SeriesId) keeps the classic behavior
            if (!current.IsRecurring || current.SeriesId == null)
            {
                await UpdateThisAsync(current, dto, tz);
                await _db.SaveChangesAsync();
                return Ok(current);
            }

            // Recurring series: we persist ONLY ONE anchor row in Appointments,
            // and store per-occurrence edits/deletes as exceptions.
            if (dto.Scope == RecurrenceScope.This)
            {
                if (!dto.OccurrenceStart.HasValue)
                    return BadRequest("OccurrenceStart é obrigatório para Scope=This em séries recorrentes.");

                await UpsertExceptionOverrideAsync(current, dto, tz);
                await _db.SaveChangesAsync();
                return Ok(current);
            }

            if (dto.Scope == RecurrenceScope.ThisAndFollowing)
            {
                if (!dto.OccurrenceStart.HasValue)
                    return BadRequest("OccurrenceStart é obrigatório para Scope=ThisAndFollowing em séries recorrentes.");

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
        public async Task<IActionResult> Delete(
            int id,
            [FromQuery] RecurrenceScope scope = RecurrenceScope.This,
            [FromQuery] DateTime? occurrenceStart = null,
            [FromQuery] DateTime? occurrenceEnd = null)
        {
            var current = await _db.Set<Appointment>().FindAsync(id);
            if (current == null) return NotFound();

            // Non-recurring: classic delete
            if (!current.IsRecurring || current.SeriesId == null)
            {
                _db.Set<Appointment>().Remove(current);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            var seriesId = current.SeriesId.Value;

            if (scope == RecurrenceScope.All)
            {
                var exAll = await _db.Set<AppointmentRecurrenceException>()
                    .Where(e => e.SeriesId == seriesId)
                    .ToListAsync();

                _db.Set<AppointmentRecurrenceException>().RemoveRange(exAll);
                _db.Set<Appointment>().Remove(current);

                await _db.SaveChangesAsync();
                return NoContent();
            }

            if (!occurrenceStart.HasValue)
                return BadRequest("occurrenceStart é obrigatório para scope This ou ThisAndFollowing em séries recorrentes.");

            if (scope == RecurrenceScope.This)
            {
                await UpsertExceptionCancellationAsync(current, occurrenceStart.Value, occurrenceEnd);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            if (scope == RecurrenceScope.ThisAndFollowing)
            {
                await CutSeriesAsync(current, occurrenceStart.Value);
                await _db.SaveChangesAsync();
                return NoContent();
            }

            return BadRequest("Invalid scope.");
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

        

        [HttpGet("series/{seriesId:guid}/exceptions")]
        public async Task<IActionResult> GetSeriesExceptions(Guid seriesId)
        {
            var exceptions = await _db.Set<AppointmentRecurrenceException>()
                .Where(e => e.SeriesId == seriesId)
                .OrderBy(e => e.OccurrenceStart)
                .ToListAsync();

            return Ok(exceptions);
        }


/// <summary>
/// Endpoint de leitura para calendário: retorna eventos normais + ocorrências recorrentes EXPANDIDAS
/// no intervalo informado, já com exceções (edit/cancel) aplicadas.
///
/// - Eventos normais retornam AppointmentId
/// - Ocorrências recorrentes retornam InstanceId (rec_{seriesIdN}_{ticks})
/// </summary>
[HttpGet("calendar")]
public async Task<IActionResult> GetCalendar(
    [FromQuery] DateTime start,
    [FromQuery] DateTime end,
    [FromQuery] int? companyId = null,
    [FromQuery] int? teamId = null,
    [FromQuery] int? customerId = null)
{
    if (end <= start) return BadRequest("end deve ser maior que start.");

    var rangeStart = start;
    var rangeEnd = end;

    // 1) Eventos não recorrentes (normais)
    var normalQuery = _db.Set<Appointment>().AsNoTracking()
        .Where(a => !a.IsRecurring && a.Start < rangeEnd && a.End > rangeStart);

    if (companyId.HasValue) normalQuery = normalQuery.Where(a => a.CompanyId == companyId.Value);
    if (teamId.HasValue) normalQuery = normalQuery.Where(a => a.TeamId == teamId.Value);
    if (customerId.HasValue) normalQuery = normalQuery.Where(a => a.CustomerId == customerId.Value);

    var normals = await normalQuery.ToListAsync();

    // 2) Âncoras recorrentes
    var anchorsQuery = _db.Set<Appointment>().AsNoTracking()
        .Where(a => a.IsRecurring
                 && a.SeriesId != null
                 && !string.IsNullOrWhiteSpace(a.RecurrenceRule)
                 && a.Start <= rangeEnd
                 && (!a.RecurrenceEnd.HasValue || a.RecurrenceEnd.Value >= rangeStart));

    if (companyId.HasValue) anchorsQuery = anchorsQuery.Where(a => a.CompanyId == companyId.Value);
    if (teamId.HasValue) anchorsQuery = anchorsQuery.Where(a => a.TeamId == teamId.Value);
    if (customerId.HasValue) anchorsQuery = anchorsQuery.Where(a => a.CustomerId == customerId.Value);

    var anchors = await anchorsQuery.ToListAsync();
    var seriesIds = anchors.Select(a => a.SeriesId!.Value).Distinct().ToList();

    // 3) Exceções (somente do intervalo — com buffer pra não perder overrides próximos)
    var exStart = rangeStart.AddDays(-7);
    var exEnd = rangeEnd.AddDays(7);

    var exceptions = await _db.Set<AppointmentRecurrenceException>().AsNoTracking()
        .Where(e => seriesIds.Contains(e.SeriesId)
                 && e.OccurrenceStart <= exEnd
                 && e.OccurrenceEnd >= exStart)
        .OrderBy(e => e.SeriesId)
        .ThenBy(e => e.OccurrenceStart)
        .ToListAsync();

    var exMap = exceptions
        .GroupBy(e => (e.SeriesId, e.OccurrenceStart))
        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedDate).First());

    var outList = new List<CalendarOccurrenceDTO>();

    // Normal -> Calendar DTO
    foreach (var a in normals)
    {
        outList.Add(new CalendarOccurrenceDTO
        {
            Id = a.Id.ToString(),
            AppointmentId = a.Id,
            IsVirtualOccurrence = false,
            IsRecurring = false,

            Start = a.Start,
            End = a.End,
            Title = a.Title,
            Address = a.Address,
            Notes = a.Notes,

            CompanyId = a.CompanyId,
            CustomerId = a.CustomerId,
            TeamId = a.TeamId,
            Status = a.Status,
            Type = a.Type,
            ProfessionalIds = a.ProfessionalIds?.ToList() ?? new List<int>()
        });
    }

    // Recorrentes -> expand + apply exceptions
    foreach (var anchor in anchors)
    {
        var tz = ResolveTimeZone(anchor.TimeZoneId);

        // Limita geração ao rangeEnd (ou RecurrenceEnd, se menor)
        DateTime? seriesEnd = anchor.RecurrenceEnd.HasValue
            ? (anchor.RecurrenceEnd.Value < rangeEnd ? anchor.RecurrenceEnd.Value : rangeEnd)
            : rangeEnd;

        var occs = ExpandOccurrences(
            anchor.RecurrenceRule!,
            anchor.Start,
            anchor.End,
            seriesEnd,
            anchor.OccurrenceCount,
            tz);

        foreach (var (occStart, occEnd) in occs)
        {
            // filtra por interseção com o range
            if (occStart >= rangeEnd || occEnd <= rangeStart) continue;

            var key = (anchor.SeriesId!.Value, occStart);
            if (exMap.TryGetValue(key, out var ex))
            {
                if (ex.IsCancelled)
                    continue; // cancelado -> não aparece no calendário

                var startFinal = ex.OverrideStart ?? occStart;
                var endFinal = ex.OverrideEnd ?? occEnd;

                // após override, ainda precisa intersectar o range
                if (startFinal >= rangeEnd || endFinal <= rangeStart) continue;

                var instId = EncodeInstanceId(anchor.SeriesId!.Value, occStart);

                outList.Add(new CalendarOccurrenceDTO
                {
                    Id = instId,
                    InstanceId = instId,
                    IsVirtualOccurrence = true,
                    IsRecurring = true,
                    AnchorAppointmentId = anchor.Id,
                    SeriesId = anchor.SeriesId,

                    Start = startFinal,
                    End = endFinal,
                    Title = ex.OverrideTitle ?? anchor.Title,
                    Address = ex.OverrideAddress ?? anchor.Address,
                    Notes = ex.OverrideNotes ?? anchor.Notes,

                    CompanyId = anchor.CompanyId,
                    CustomerId = anchor.CustomerId,
                    TeamId = anchor.TeamId,
                    Status = ex.OverrideStatus ?? anchor.Status,
                    Type = ex.OverrideType ?? anchor.Type,

                    ProfessionalIds = ex.OverrideProfessionalIds?.ToList()
                        ?? anchor.ProfessionalIds?.ToList()
                        ?? new List<int>(),

                    HasOverride = true
                });

                continue;
            }

            // sem exceção
            var instanceId = EncodeInstanceId(anchor.SeriesId!.Value, occStart);

            outList.Add(new CalendarOccurrenceDTO
            {
                Id = instanceId,
                InstanceId = instanceId,
                IsVirtualOccurrence = true,
                IsRecurring = true,
                AnchorAppointmentId = anchor.Id,
                SeriesId = anchor.SeriesId,

                Start = occStart,
                End = occEnd,

                Title = anchor.Title,
                Address = anchor.Address,
                Notes = anchor.Notes,

                CompanyId = anchor.CompanyId,
                CustomerId = anchor.CustomerId,
                TeamId = anchor.TeamId,
                Status = anchor.Status,
                Type = anchor.Type,

                ProfessionalIds = anchor.ProfessionalIds?.ToList() ?? new List<int>()
            });
        }
    }

    return Ok(outList.OrderBy(x => x.Start).ToList());
}

/// <summary>
/// Atualiza uma ocorrência recorrente por InstanceId (sem o front precisar calcular OccurrenceStart).
/// scope: This / ThisAndFollowing / All
/// </summary>
[HttpPut("instance/{instanceId}")]
public async Task<IActionResult> UpdateInstance(string instanceId, [FromBody] UpdateAppointmentDTO dto)
{
    if (!TryDecodeInstanceId(instanceId, out var seriesId, out var occStart))
        return BadRequest("InstanceId inválido.");

    var anchor = await _db.Set<Appointment>()
        .FirstOrDefaultAsync(a => a.IsRecurring && a.SeriesId == seriesId);

    if (anchor == null) return NotFound();

    // Força a identificação da ocorrência clicada
    dto.OccurrenceStart = occStart;

    // Reaproveita a lógica de Update por ID
    return await Update(anchor.Id, dto);
}

/// <summary>
/// Deleta uma ocorrência recorrente por InstanceId (sem o front precisar calcular OccurrenceStart).
/// </summary>
[HttpDelete("instance/{instanceId}")]
public async Task<IActionResult> DeleteInstance(
    string instanceId,
    [FromQuery] RecurrenceScope scope = RecurrenceScope.This)
{
    if (!TryDecodeInstanceId(instanceId, out var seriesId, out var occStart))
        return BadRequest("InstanceId inválido.");

    var anchor = await _db.Set<Appointment>()
        .FirstOrDefaultAsync(a => a.IsRecurring && a.SeriesId == seriesId);

    if (anchor == null) return NotFound();

    return await Delete(anchor.Id, scope, occStart, null);
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



        private (DateTime occStart, DateTime occEnd) ResolveOccurrenceWindow(
            Appointment anchor, DateTime occurrenceStart, DateTime? occurrenceEndOverride)
        {
            var duration = anchor.End - anchor.Start;
            var occEnd = occurrenceEndOverride ?? (occurrenceStart + duration);
            return (occurrenceStart, occEnd);
        }

        private async Task UpsertExceptionCancellationAsync(
            Appointment anchor, DateTime occurrenceStart, DateTime? occurrenceEndOverride)
        {
            var seriesId = anchor.SeriesId!.Value;
            var (occStart, occEnd) = ResolveOccurrenceWindow(anchor, occurrenceStart, occurrenceEndOverride);

            var ex = await _db.Set<AppointmentRecurrenceException>()
                .FirstOrDefaultAsync(e => e.SeriesId == seriesId && e.OccurrenceStart == occStart);

            if (ex == null)
            {
                ex = new AppointmentRecurrenceException
                {
                    SeriesId = seriesId,
                    OccurrenceStart = occStart,
                    OccurrenceEnd = occEnd,
                    IsCancelled = true
                };
                await _db.Set<AppointmentRecurrenceException>().AddAsync(ex);
            }
            else
            {
                ex.OccurrenceEnd = occEnd;
                ex.IsCancelled = true;
            }
        }

        private async Task UpsertExceptionOverrideAsync(
            Appointment anchor, UpdateAppointmentDTO dto, TimeZoneInfo tz)
        {
            var seriesId = anchor.SeriesId!.Value;
            var occStart = dto.OccurrenceStart!.Value;
            var (windowStart, windowEnd) = ResolveOccurrenceWindow(anchor, occStart, dto.OccurrenceEnd);

            var ex = await _db.Set<AppointmentRecurrenceException>()
                .FirstOrDefaultAsync(e => e.SeriesId == seriesId && e.OccurrenceStart == windowStart);

            if (ex == null)
            {
                ex = new AppointmentRecurrenceException
                {
                    SeriesId = seriesId,
                    OccurrenceStart = windowStart,
                    OccurrenceEnd = windowEnd,
                    IsCancelled = false
                };
                await _db.Set<AppointmentRecurrenceException>().AddAsync(ex);
            }
            else
            {
                ex.OccurrenceEnd = windowEnd;
                ex.IsCancelled = false;
            }

            if (dto.Title != null) ex.OverrideTitle = dto.Title;
            if (dto.Address != null) ex.OverrideAddress = dto.Address;
            if (dto.Notes != null) ex.OverrideNotes = dto.Notes;

            if (dto.Start.HasValue && dto.End.HasValue)
            {
                ex.OverrideStart = dto.Start.Value;
                ex.OverrideEnd = dto.End.Value;
            }

            if (dto.Status.HasValue) ex.OverrideStatus = dto.Status.Value;
            if (dto.Type.HasValue) ex.OverrideType = dto.Type.Value;

            if (dto.ProfessionalIds != null)
                ex.OverrideProfessionalIds = dto.ProfessionalIds.Distinct().ToList();
        }

        private async Task CutSeriesAsync(Appointment anchor, DateTime occurrenceStart)
        {
            var seriesId = anchor.SeriesId!.Value;

            // If the cut is at/before the first occurrence, delete the whole series
            if (occurrenceStart <= anchor.Start)
            {
                var exAll = await _db.Set<AppointmentRecurrenceException>()
                    .Where(e => e.SeriesId == seriesId)
                    .ToListAsync();

                _db.Set<AppointmentRecurrenceException>().RemoveRange(exAll);
                _db.Set<Appointment>().Remove(anchor);
                return;
            }

            var cutEnd = occurrenceStart.AddTicks(-1);

            if (!anchor.RecurrenceEnd.HasValue || anchor.RecurrenceEnd.Value > cutEnd)
                anchor.RecurrenceEnd = cutEnd;

            // Prefer end-date bounded series after a cut
            anchor.OccurrenceCount = null;

            // Remove exceptions that are now beyond the new end
            var future = await _db.Set<AppointmentRecurrenceException>()
                .Where(e => e.SeriesId == seriesId && e.OccurrenceStart >= occurrenceStart)
                .ToListAsync();

            _db.Set<AppointmentRecurrenceException>().RemoveRange(future);
        }
        private Appointment MapAppointment(
            CreateAppointmentDTO dto, DateTime start, DateTime end, TimeZoneInfo tz, bool isRecurring, Guid? seriesId)
        {
            var appointment = new Appointment
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
                Status = dto.Status ?? Core.Enums.Appointment.AppointmentStatus.Scheduled,
                Type   = dto.Type   ?? Core.Enums.Appointment.AppointmentType.Regular,
                IsRecurring = isRecurring,
                RecurrenceRule = dto.RecurrenceRule,
                SeriesId = seriesId,
                RecurrenceEnd = dto.RecurrenceEnd,
                OccurrenceCount = dto.OccurrenceCount,
                IsException = false
            };

            // Atribui lista de profissionais, se enviada
            if (dto.ProfessionalIds != null)
            {
                appointment.ProfessionalIds = dto.ProfessionalIds.Distinct().ToList();
            }

            return appointment;
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
            // "ThisAndFollowing" in a single-row series model:
            // We SPLIT the series into two series:
            // - a new "previous" anchor keeps the OLD SeriesId and is cut to end BEFORE occurrenceStart
            // - the current anchor keeps the SAME database Id, but receives a NEW SeriesId and starts at occurrenceStart
            // This keeps "1 row per series" while preserving the past.

            if (anchor.SeriesId == null)
            {
                await UpdateThisAsync(anchor, dto, tz);
                return;
            }

            if (!dto.OccurrenceStart.HasValue)
                throw new InvalidOperationException("OccurrenceStart é obrigatório para Scope=ThisAndFollowing.");

            var occStart = dto.OccurrenceStart.Value;

            // If the split point is at or before the first occurrence, treat as "All"
            if (occStart <= anchor.Start)
            {
                await UpdateAllAsync(anchor, dto, tz);
                return;
            }

            var oldSeriesId = anchor.SeriesId.Value;
            var newSeriesId = Guid.NewGuid();

            // Clone current anchor as the "previous" series (past)
            var previous = new Appointment
            {
                Title = anchor.Title,
                Address = anchor.Address,
                Notes = anchor.Notes,
                Start = anchor.Start,
                End = anchor.End,
                TimeZoneId = anchor.TimeZoneId,
                CompanyId = anchor.CompanyId,
                CustomerId = anchor.CustomerId,
                TeamId = anchor.TeamId,
                Status = anchor.Status,
                Type = anchor.Type,
                ProfessionalIdsData = anchor.ProfessionalIdsData,

                IsRecurring = true,
                RecurrenceRule = anchor.RecurrenceRule,
                SeriesId = oldSeriesId,
                RecurrenceEnd = occStart.AddTicks(-1),
                OccurrenceCount = null,
                IsException = false
            };

            await _db.Set<Appointment>().AddAsync(previous);

            // Move future exceptions to the new series id
            var futureExceptions = await _db.Set<AppointmentRecurrenceException>()
                .Where(e => e.SeriesId == oldSeriesId && e.OccurrenceStart >= occStart)
                .ToListAsync();

            foreach (var ex in futureExceptions)
                ex.SeriesId = newSeriesId;

            // Update current anchor to become the "future" series
            var duration = anchor.End - anchor.Start;

            anchor.SeriesId = newSeriesId;
            anchor.Start = dto.Start ?? occStart;
            anchor.End = dto.End ?? (anchor.Start + duration);

            // Apply remaining updates to the (new) series anchor
            await UpdateAllAsync(anchor, dto, tz);
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

private static string EncodeInstanceId(Guid seriesId, DateTime occurrenceStart)
{
    // occurrenceStart é armazenado como horário local/unspecified no padrão do projeto
    return $"rec_{seriesId:N}_{occurrenceStart.Ticks}";
}

private static bool TryDecodeInstanceId(string instanceId, out Guid seriesId, out DateTime occurrenceStart)
{
    seriesId = default;
    occurrenceStart = default;

    if (string.IsNullOrWhiteSpace(instanceId)) return false;
    var parts = instanceId.Split('_', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 3) return false;
    if (!parts[0].Equals("rec", StringComparison.OrdinalIgnoreCase)) return false;

    if (!Guid.TryParseExact(parts[1], "N", out seriesId)) return false;
    if (!long.TryParse(parts[2], out var ticks)) return false;

    try
    {
        occurrenceStart = new DateTime(ticks, DateTimeKind.Unspecified);
        return true;
    }
    catch
    {
        return false;
    }
}


    }
}