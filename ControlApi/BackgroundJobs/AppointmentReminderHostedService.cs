using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Enums.Appointment;
using Core.Enums.Notifications;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Job automático: envia lembrete 30 minutos antes do início do agendamento.
    ///
    /// - Usa AppointmentReminderDispatches para idempotência.
    /// - Considera User.Language (pt-br => PT; qualquer outro => EN).
    /// - Para recorrência, expande apenas as ocorrências dentro da janela.
    /// </summary>
    public class AppointmentReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentReminderHostedService> _logger;

        // Intervalo do job.
        private static readonly TimeSpan LoopDelay = TimeSpan.FromSeconds(60);

        public AppointmentReminderHostedService(IServiceScopeFactory scopeFactory, ILogger<AppointmentReminderHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Pequeno atraso para a API subir com tudo pronto
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Reminders] Erro inesperado no job de lembretes.");
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var nowUtc = DateTime.UtcNow;

            // Janela de disparo do lembrete.
            // Não usamos "igualdade" exata com +30min porque:
            // - o loop roda em intervalos de 60s e pode driftar;
            // - o servidor pode estar em UTC e os agendamentos são salvos em horário local.
            // A idempotência (AppointmentReminderDispatches) garante que ampliar a janela não duplica envios.
            var windowStartUtc = nowUtc.AddMinutes(28); // 30min - 2min
            var windowEndUtc = nowUtc.AddMinutes(32);   // 30min + 2min
            _logger.LogInformation(
                "[Reminders] Tick nowUtc={NowUtc:o} windowStartUtc={Ws:o} windowEndUtc={We:o}",
                nowUtc, windowStartUtc, windowEndUtc);

            // ---------------
            // 1) Não recorrentes
            // ---------------
            // Como o sistema pode rodar em múltiplos fusos, buscamos candidatos numa janela ampla
            // e depois convertemos cada Start para UTC com base no TimeZoneId do appointment (quando existir).
            var nonRecurringCandidates = await db.Appointments.AsNoTracking()
                .Where(a => !a.IsRecurring
                            && a.Status == AppointmentStatus.Scheduled
                            && a.Start >= nowUtc.AddHours(-24)
                            && a.Start <= nowUtc.AddHours(24))
                .ToListAsync(ct);

            _logger.LogInformation("[Reminders] Non-recurring candidates={Count}", nonRecurringCandidates.Count);

            foreach (var a in nonRecurringCandidates)
            {
                var tz = ResolveTimeZoneSafe(a.TimeZoneId);
                DateTime startUtc;
                if (string.IsNullOrWhiteSpace(a.TimeZoneId))
                {
                    // Se não temos fuso, assumimos que Start já está em UTC
                    startUtc = DateTime.SpecifyKind(a.Start, DateTimeKind.Utc);
                }
                else
                {
                    startUtc = LocalToUtc(a.Start, tz);
                }

                if (startUtc < windowStartUtc || startUtc >= windowEndUtc)
                    continue;

                _logger.LogInformation(
                    "[Reminders] Match non-recurring appointmentId={Id} startLocal={StartLocal:o} startUtc={StartUtc:o} tz={Tz}",
                    a.Id, a.Start, startUtc, tz.Id);

                await SendReminderForOccurrenceAsync(
                    db,
                    notificationService,
                    appointmentAnchor: a,
                    seriesId: null,
                    occurrenceStartLocal: a.Start,
                    occurrenceStartUtc: startUtc,
                    professionalIds: a.ProfessionalIds,
                    ct);
            }

            // ---------------
            // 2) Recorrentes (âncoras + expansão no range)
            // ---------------
            var recurringAnchors = await db.Appointments.AsNoTracking()
                .Where(a => a.IsRecurring
                            && a.SeriesId != null
                            && !string.IsNullOrWhiteSpace(a.RecurrenceRule)
                            && a.Status == AppointmentStatus.Scheduled)
                .ToListAsync(ct);

            foreach (var anchor in recurringAnchors)
            {
                var tz = ResolveTimeZoneSafe(anchor.TimeZoneId);
                var rangeStartLocal = TimeZoneInfo.ConvertTimeFromUtc(windowStartUtc, tz);
                var rangeEndLocal = TimeZoneInfo.ConvertTimeFromUtc(windowEndUtc, tz);

                // Expandimos uma janela um pouco maior para capturar pequenas derivações e overrides.
                var expandStartLocal = rangeStartLocal.AddMinutes(-2);
                var expandEndLocal = rangeEndLocal.AddMinutes(2);

                // Carrega exceções relevantes (por OccurrenceStart e por OverrideStart)
                var seriesId = anchor.SeriesId!.Value;
                var exceptions = await db.AppointmentRecurrenceExceptions.AsNoTracking()
                    .Where(e => e.SeriesId == seriesId &&
                                ((e.OccurrenceStart >= expandStartLocal.AddDays(-1) && e.OccurrenceStart <= expandEndLocal.AddDays(1))
                                 || (e.OverrideStart.HasValue && e.OverrideStart.Value >= expandStartLocal && e.OverrideStart.Value <= expandEndLocal)))
                    .ToListAsync(ct);

                var exByOccurrence = exceptions
                    .GroupBy(e => e.OccurrenceStart)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedDate).First());

                if (string.IsNullOrWhiteSpace(anchor.RecurrenceRule))
                    continue;

                // Série end (local): RecurrenceEnd é armazenado como DateTime (no banco), normalmente no fuso da série.
                DateTime? seriesEndLocal = anchor.RecurrenceEnd;

                foreach (var occ in RRuleExpander.ExpandInRange(
                             anchor.RecurrenceRule!,
                             anchor.Start,
                             anchor.End,
                             seriesEndLocal,
                             anchor.OccurrenceCount,
                             expandStartLocal,
                             expandEndLocal))
                {
                    // Aplica exceção/cancelamento
                    if (exByOccurrence.TryGetValue(occ.startLocal, out var ex))
                    {
                        if (ex.IsCancelled)
                            continue;

                        var effectiveStart = ex.OverrideStart ?? occ.startLocal;
                        var effectiveProIds = (ex.OverrideProfessionalIds != null && ex.OverrideProfessionalIds.Any())
                            ? ex.OverrideProfessionalIds
                            : anchor.ProfessionalIds;

                        var effectiveStartUtc = LocalToUtc(effectiveStart, tz);

                        if (effectiveStartUtc < windowStartUtc || effectiveStartUtc >= windowEndUtc)
                            continue;

                        await SendReminderForOccurrenceAsync(
                            db,
                            notificationService,
                            appointmentAnchor: anchor,
                            seriesId: seriesId,
                            occurrenceStartLocal: effectiveStart,
                            occurrenceStartUtc: effectiveStartUtc,
                            professionalIds: effectiveProIds,
                            ct);

                        continue;
                    }

                    // Sem exceção
                    var occStartUtc = LocalToUtc(occ.startLocal, tz);
                    if (occStartUtc < windowStartUtc || occStartUtc >= windowEndUtc)
                        continue;

                    await SendReminderForOccurrenceAsync(
                        db,
                        notificationService,
                        appointmentAnchor: anchor,
                        seriesId: seriesId,
                        occurrenceStartLocal: occ.startLocal,
                        occurrenceStartUtc: occStartUtc,
                        professionalIds: anchor.ProfessionalIds,
                        ct);
                }
            }
        }

        private async Task SendReminderForOccurrenceAsync(
            DbContextClass db,
            INotificationService notificationService,
            Appointment appointmentAnchor,
            Guid? seriesId,
            DateTime occurrenceStartLocal,
            DateTime occurrenceStartUtc,
            List<int> professionalIds,
            CancellationToken ct)
        {
            if (professionalIds == null || professionalIds.Count == 0)
            {
                _logger.LogDebug("[Reminders] Skip appointmentId={Id}: no professionalIds", appointmentAnchor.Id);
                return;
            }

            // Mapeia ProfessionalIds -> Users
            var proIds = professionalIds.Distinct().ToList();
            var users = await db.Users.AsNoTracking()
                .Where(u => u.ProfessionalId.HasValue && proIds.Contains(u.ProfessionalId.Value))
                .ToListAsync(ct);

            if (users.Count == 0)
            {
                _logger.LogWarning(
                    "[Reminders] No users mapped for appointmentId={Id} seriesId={SeriesId} proIds=[{ProIds}]",
                    appointmentAnchor.Id, seriesId, string.Join(",", proIds));
                return;
            }

            // Filtra usuários elegíveis (ativos)
            users = users
                .Where(u => u.Status == Core.Enums.StatusEnum.Active
                            && string.Equals(u.Role, "professional", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (users.Count == 0)
            {
                _logger.LogWarning(
                    "[Reminders] No eligible professional users for appointmentId={Id} seriesId={SeriesId}",
                    appointmentAnchor.Id, seriesId);
                return;
            }

            // Idempotência: remove os que já receberam
            var recipientIds = users.Select(u => u.Id).Distinct().ToList();
            var already = await db.AppointmentReminderDispatches.AsNoTracking()
                .Where(d => d.AppointmentId == appointmentAnchor.Id
                            && d.SeriesId == seriesId
                            && d.OccurrenceStartUtc == occurrenceStartUtc
                            && d.ReminderType == Core.Enums.Notifications.ReminderType.Minutes30Before
                            && recipientIds.Contains(d.RecipientUserId))
                .Select(d => d.RecipientUserId)
                .ToListAsync(ct);

            var toSendUsers = users.Where(u => !already.Contains(u.Id)).ToList();
            if (toSendUsers.Count == 0)
            {
                _logger.LogDebug(
                    "[Reminders] Already dispatched appointmentId={Id} seriesId={SeriesId} startUtc={StartUtc:o}",
                    appointmentAnchor.Id, seriesId, occurrenceStartUtc);
                return;
            }

            // Cria logs de dispatch antes (idempotência via índice único)
            var dispatchRows = toSendUsers.Select(u => new AppointmentReminderDispatch
            {
                AppointmentId = appointmentAnchor.Id,
                SeriesId = seriesId,
                OccurrenceStartUtc = occurrenceStartUtc,
                RecipientUserId = u.Id,
                ReminderType = Core.Enums.Notifications.ReminderType.Minutes30Before
            }).ToList();

            db.AppointmentReminderDispatches.AddRange(dispatchRows);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Se houver corrida/duplicata por índice único, seguimos e deixamos a próxima rodada consolidar.
            }

            _logger.LogInformation(
                "[Reminders] Dispatching reminder appointmentId={Id} seriesId={SeriesId} startLocal={StartLocal:o} startUtc={StartUtc:o} recipients={Recipients}",
                appointmentAnchor.Id,
                seriesId,
                occurrenceStartLocal,
                occurrenceStartUtc,
                string.Join(",", toSendUsers.Select(u => u.Id)));

            // Agrupa por idioma para gerar mensagens
            var ptUsers = toSendUsers.Where(IsPtBr).Select(u => u.Id).ToList();
            var enUsers = toSendUsers.Where(u => !IsPtBr(u)).Select(u => u.Id).ToList();

            if (ptUsers.Count > 0)
            {
                await notificationService.CreateAsync(new CreateNotificationDTO
                {
                    Title = "Agendamento em 30 minutos",
                    Message = BuildMessagePt(appointmentAnchor, occurrenceStartLocal),
                    Type = "Info",
                    RecipientRole = "Professional",
                    IsBroadcast = false,
                    UserIds = ptUsers,
                    CompanyId = appointmentAnchor.CompanyId
                });

                _logger.LogInformation(
                    "[Reminders] Created PT notifications appointmentId={Id} users=[{Users}]",
                    appointmentAnchor.Id, string.Join(",", ptUsers));
            }

            if (enUsers.Count > 0)
            {
                await notificationService.CreateAsync(new CreateNotificationDTO
                {
                    Title = "Appointment in 30 minutes",
                    Message = BuildMessageEn(appointmentAnchor, occurrenceStartLocal),
                    Type = "Info",
                    RecipientRole = "Professional",
                    IsBroadcast = false,
                    UserIds = enUsers,
                    CompanyId = appointmentAnchor.CompanyId
                });

                _logger.LogInformation(
                    "[Reminders] Created EN notifications appointmentId={Id} users=[{Users}]",
                    appointmentAnchor.Id, string.Join(",", enUsers));
            }
        }

        private static bool IsPtBr(User u)
        {
            var lang = (u.Language ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lang)) return true; // padrão PT
            return lang.StartsWith("pt") || lang == "pt-br" || lang == "ptbr";
        }

        private static string BuildMessagePt(Appointment a, DateTime startLocal)
        {
            _ = startLocal;
            var title = string.IsNullOrWhiteSpace(a.Title) ? "Seu agendamento" : a.Title;

            if (!string.IsNullOrWhiteSpace(a.Address))
                return $"{title}: começa em 30 minutos. Endereço: {a.Address}.";

            return $"{title}: começa em 30 minutos.";
        }

        private static string BuildMessageEn(Appointment a, DateTime startLocal)
        {
            _ = startLocal;
            var title = string.IsNullOrWhiteSpace(a.Title) ? "Your appointment" : a.Title;

            if (!string.IsNullOrWhiteSpace(a.Address))
                return $"{title}: starts in 30 minutes. Address: {a.Address}.";

            return $"{title}: starts in 30 minutes.";
        }

        private static TimeZoneInfo ResolveTimeZoneSafe(string? timeZoneId)
        {
            // Não fixamos em um fuso específico: se vier TimeZoneId válido, usamos; caso contrário, UTC.
            if (string.IsNullOrWhiteSpace(timeZoneId))
                return TimeZoneInfo.Utc;

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static DateTime LocalToUtc(DateTime local, TimeZoneInfo tz)
        {
            // Treat local as Unspecified to avoid accidental double conversion.
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        }
    }

    internal static class RRuleExpander
    {
        // Expande apenas dentro do range pedido (não gera a série inteira)
        public static IEnumerable<(DateTime startLocal, DateTime endLocal)> ExpandInRange(
            string rrule,
            DateTime startLocal,
            DateTime endLocal,
            DateTime? endLocalSeries,
            int? count,
            DateTime rangeStartLocal,
            DateTime rangeEndLocal)
        {
            var rule = ParseRRule(rrule);
            var duration = endLocal - startLocal;

            // Limite superior da série
            var seriesLimit = endLocalSeries ?? startLocal.AddYears(2);
            if (seriesLimit > rangeEndLocal) seriesLimit = rangeEndLocal;

            var occurrences = 0;
            var timeOfDay = startLocal.TimeOfDay;

            if (rule.Freq == "DAILY")
            {
                var cursor = startLocal;
                while (cursor <= seriesLimit && (count == null || occurrences < count.Value))
                {
                    var occStart = cursor;
                    var occEnd = cursor + duration;

                    if (occStart >= rangeStartLocal && occStart <= rangeEndLocal)
                        yield return (occStart, occEnd);

                    if (occStart > rangeEndLocal)
                        yield break;

                    occurrences++;
                    cursor = cursor.AddDays(rule.Interval);
                }
                yield break;
            }

            if (rule.Freq == "WEEKLY")
            {
                var days = rule.ByDay;
                if (days.Count == 0) days = new List<string> { DayToByDay(startLocal.DayOfWeek) };
                days = days
                    .Select(d => d.ToUpperInvariant())
                    .Distinct()
                    .OrderBy(DaySortKey)
                    .ToList();

                // Começa na semana do startLocal
                var weekStart = startLocal.Date;
                while (weekStart <= seriesLimit && (count == null || occurrences < count.Value))
                {
                    foreach (var d in days)
                    {
                        var dayDate = NextOnOrAfter(weekStart, d);
                        var occStart = dayDate.Date + timeOfDay;
                        if (occStart < startLocal) continue;
                        if (occStart > seriesLimit) yield break;
                        if (count != null && occurrences >= count.Value) yield break;

                        if (occStart >= rangeStartLocal && occStart <= rangeEndLocal)
                            yield return (occStart, occStart + duration);

                        occurrences++;
                    }
                    weekStart = weekStart.AddDays(7 * rule.Interval);
                }
                yield break;
            }

            if (rule.Freq == "MONTHLY")
            {
                var monthDays = rule.ByMonthDay;
                var targetDay = monthDays.Count > 0 ? monthDays[0] : startLocal.Day;

                var monthCursor = new DateTime(startLocal.Year, startLocal.Month, 1, 0, 0, 0, startLocal.Kind);
                while (monthCursor <= seriesLimit && (count == null || occurrences < count.Value))
                {
                    var daysInMonth = DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
                    if (targetDay >= 1 && targetDay <= daysInMonth)
                    {
                        var dayDate = new DateTime(monthCursor.Year, monthCursor.Month, targetDay, 0, 0, 0, monthCursor.Kind);
                        var occStart = dayDate + timeOfDay;
                        if (occStart >= startLocal && occStart <= seriesLimit)
                        {
                            if (occStart >= rangeStartLocal && occStart <= rangeEndLocal)
                                yield return (occStart, occStart + duration);
                            occurrences++;
                        }
                    }
                    if (monthCursor > rangeEndLocal) yield break;
                    monthCursor = monthCursor.AddMonths(rule.Interval);
                }
                yield break;
            }

            // Fallback: ocorrência única
            if (startLocal >= rangeStartLocal && startLocal <= rangeEndLocal)
                yield return (startLocal, endLocal);
        }

        private class RRule
        {
            public string Freq { get; set; } = "DAILY";
            public int Interval { get; set; } = 1;
            public List<string> ByDay { get; set; } = new List<string>();
            public List<int> ByMonthDay { get; set; } = new List<int>();
        }

        private static RRule ParseRRule(string rrule)
        {
            var r = new RRule();
            var parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                var kv = p.Split('=', 2, StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;
                var key = kv[0].ToUpperInvariant();
                var val = kv[1];
                if (key == "FREQ") r.Freq = val.ToUpperInvariant();
                if (key == "INTERVAL" && int.TryParse(val, out var i)) r.Interval = Math.Max(1, i);
                if (key == "BYDAY") r.ByDay = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (key == "BYMONTHDAY")
                {
                    r.ByMonthDay = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => int.TryParse(x, out var d) ? d : 0)
                        .Where(x => x > 0)
                        .ToList();
                }
            }
            return r;
        }

        private static int DaySortKey(string byday)
        {
            return byday.ToUpperInvariant() switch
            {
                "MO" => 1,
                "TU" => 2,
                "WE" => 3,
                "TH" => 4,
                "FR" => 5,
                "SA" => 6,
                "SU" => 7,
                _ => 8
            };
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
            var map = new Dictionary<string, DayOfWeek>
            {
                ["MO"] = DayOfWeek.Monday,
                ["TU"] = DayOfWeek.Tuesday,
                ["WE"] = DayOfWeek.Wednesday,
                ["TH"] = DayOfWeek.Thursday,
                ["FR"] = DayOfWeek.Friday,
                ["SA"] = DayOfWeek.Saturday,
                ["SU"] = DayOfWeek.Sunday,
            };
            var targetDow = map.ContainsKey(target) ? map[target] : DayOfWeek.Monday;

            var diff = (int)targetDow - (int)weekStart.DayOfWeek;
            if (diff < 0) diff += 7;
            return weekStart.AddDays(diff);
        }
    }
}
