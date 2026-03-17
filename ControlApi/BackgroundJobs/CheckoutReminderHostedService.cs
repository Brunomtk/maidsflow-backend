using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Enums.Notifications;
using Core.Enums.User;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Job automático: envia push para profissionais que fizeram Check-In, mas esqueceram de fazer Check-Out.
    ///
    /// Regras:
    /// - Dispara ~10 minutos após o horário de término do appointment (janela com tolerância).
    /// - Só dispara se existir CheckInTime e NÃO existir CheckOutTime.
    /// - Usa AppointmentReminderDispatches para idempotência (não reenviar).
    /// - Notificação expira rapidamente: remove do banco após 30 minutos (cleanup a cada tick).
    /// </summary>
    public class CheckoutReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CheckoutReminderHostedService> _logger;
        private readonly IConfiguration _config;
        private readonly Services.IBackgroundJobMonitorService _jobMonitor;

        private const int DefaultTickSeconds = 60;
        private const int DefaultDelayMinutesAfterEnd = 10;
        private const int DefaultWindowToleranceMinutes = 2;
        private const int DefaultNotificationTtlMinutes = 30;

        private static readonly string[] ReminderTitles =
        {
            "Checkout pendente",
            "Checkout pending"
        };

        public CheckoutReminderHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<CheckoutReminderHostedService> logger,
            IConfiguration config,
            Services.IBackgroundJobMonitorService jobMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _config = config;
            _jobMonitor = jobMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);
            var enabled = _config.GetValue("AutoNotifications:CheckoutReminder:Enabled", true);
            if (!enabled)
            {
                _logger.LogInformation("[CheckoutReminder] Desabilitado por configuração.");
                await _jobMonitor.MarkDisabledAsync(Services.BackgroundJobKeys.CheckoutReminder, "Checkout Reminder", "Notifications", "Disabled by configuration.", null, stoppingToken);
                return;
            }

            var tickSeconds = Math.Max(10, _config.GetValue("AutoNotifications:CheckoutReminder:TickSeconds", DefaultTickSeconds));
            var tick = TimeSpan.FromSeconds(tickSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(tick);
                var run = await _jobMonitor.MarkStartedAsync(Services.BackgroundJobKeys.CheckoutReminder, "Checkout Reminder", "Notifications", nextRunUtc, stoppingToken);
                try
                {
                    var result = await RunTickAsync(stoppingToken);
                    await _jobMonitor.MarkSucceededAsync(run, result.summary, result.processed, result.succeeded, result.failed, nextRunUtc, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CheckoutReminder] Erro no tick. Tentando novamente no próximo ciclo.");
                    await _jobMonitor.MarkFailedAsync(run, ex, "Checkout reminder tick failed.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                }

                try { await Task.Delay(tick, stoppingToken); } catch (TaskCanceledException) { break; }
            }
        }

        private async Task<(int processed, int succeeded, int failed, string summary)> RunTickAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var pushSender = scope.ServiceProvider.GetRequiredService<Services.IPushNotificationSender>();

            var nowUtc = DateTime.UtcNow;

            var processed = 0;
            var succeeded = 0;

            // ---- Cleanup rápido (TTL 30min) ----
            var ttlMinutes = _config.GetValue("AutoNotifications:CheckoutReminder:NotificationTtlMinutes", DefaultNotificationTtlMinutes);
            var cutoffUtc = nowUtc.AddMinutes(-Math.Max(1, ttlMinutes));

            var expiredIds = await db.Notifications
                .Where(n => ReminderTitles.Contains(n.Title) && n.SentAt < cutoffUtc)
                .Select(n => n.Id)
                .ToListAsync(ct);

            processed += expiredIds.Count;
            if (expiredIds.Count > 0)
            {
                db.Notifications.RemoveRange(db.Notifications.Where(n => expiredIds.Contains(n.Id)));
                await db.SaveChangesAsync(ct);
                succeeded += expiredIds.Count;
                _logger.LogInformation("[CheckoutReminder] Removidas {Count} notificações expiradas (cutoff {CutoffUtc:o}).", expiredIds.Count, cutoffUtc);
            }

            // ---- Janela de disparo ----
            var delayMinutes = _config.GetValue("AutoNotifications:CheckoutReminder:DelayMinutesAfterEnd", DefaultDelayMinutesAfterEnd);
            var tolMinutes = _config.GetValue("AutoNotifications:CheckoutReminder:WindowToleranceMinutes", DefaultWindowToleranceMinutes);

            // Queremos: (appointment.End + delay) ∈ [now - tol, now + tol]
            // => appointment.End ∈ [now - delay - tol, now - delay + tol]
            var endFromUtc = nowUtc.AddMinutes(-(delayMinutes + tolMinutes));
            var endToUtc = nowUtc.AddMinutes(-(delayMinutes - tolMinutes));

            // Buscar check-records em aberto (check-in feito, check-out pendente) cujos appointments terminaram na janela.
            // JOIN por AppointmentId para filtrar por End.
            var openChecks = await (
                    from cr in db.CheckRecords.AsNoTracking()
                    join a in db.Appointments.AsNoTracking() on cr.AppointmentId equals a.Id
                    where cr.CheckInTime != null
                          && cr.CheckOutTime == null
                          && a.End >= endFromUtc
                          && a.End <= endToUtc
                          && a.Status != Core.Enums.Appointment.AppointmentStatus.Cancelled
                    select new
                    {
                        Check = cr,
                        Appointment = a
                    }
                )
                .ToListAsync(ct);

            processed += openChecks.Count;
            if (openChecks.Count == 0)
            {
                _logger.LogDebug("[CheckoutReminder] Nenhum check-in pendente na janela. endFromUtc={From:o} endToUtc={To:o}", endFromUtc, endToUtc);
                return (processed, succeeded, 0, "No open checkout reminders in current window.");
            }

            // Carregar users profissionais relacionados
            var professionalIds = openChecks.Select(x => x.Check.ProfessionalId).Distinct().ToList();

            var users = await db.Users.AsNoTracking()
                .Where(u => professionalIds.Contains(u.ProfessionalId ?? 0) && u.Role.ToLower() == "professional")
                .Select(u => new { u.Id, u.ProfessionalId, u.Language, u.CompanyId })
                .ToListAsync(ct);

            var userByProfessionalId = users
                .Where(u => u.ProfessionalId.HasValue)
                .GroupBy(u => u.ProfessionalId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            // Pré-carregar dispatches já enviados nessa janela (para idempotência)
            var appointmentIds = openChecks.Select(x => x.Appointment.Id).Distinct().ToList();

            var existingDispatches = await db.AppointmentReminderDispatches.AsNoTracking()
                .Where(d => appointmentIds.Contains(d.AppointmentId)
                            && d.ReminderType == ReminderType.CheckoutMissingAfterEnd10Min)
                .ToListAsync(ct);

            var existingSet = existingDispatches
                .Select(d => $"{d.AppointmentId}:{d.RecipientUserId}:{d.OccurrenceStartUtc:o}")
                .ToHashSet();

            var createdNotifications = new List<Notification>();
            var createdDispatches = new List<AppointmentReminderDispatch>();

            foreach (var item in openChecks)
            {
                if (!userByProfessionalId.TryGetValue(item.Check.ProfessionalId, out var u))
                    continue;

                var occurrenceStartUtc = item.Appointment.Start;

                var key = $"{item.Appointment.Id}:{u.Id}:{occurrenceStartUtc:o}";
                if (existingSet.Contains(key))
                    continue;

                var isPt = IsPtBr(u.Language);

                var title = isPt ? "Checkout pendente" : "Checkout pending";
                var msg = BuildMessage(isPt, item.Appointment);

                var notif = new Notification
                {
                    Title = title,
                    Message = msg,
                    Type = NotificationType.Warning,
                    RecipientId = u.Id,
                    RecipientRole = UserRole.Professional,
                    CompanyId = item.Appointment.CompanyId,
                    UserId = u.Id,
                    ProfessionalId = item.Check.ProfessionalId,
                    Status = Core.Enums.Notifications.NotificationStatus.Unread,
                    SentAt = nowUtc
                };

                createdNotifications.Add(notif);

                createdDispatches.Add(new AppointmentReminderDispatch
                {
                    AppointmentId = item.Appointment.Id,
                    SeriesId = item.Appointment.SeriesId,
                    OccurrenceStartUtc = occurrenceStartUtc,
                    RecipientUserId = u.Id,
                    ReminderType = ReminderType.CheckoutMissingAfterEnd10Min
                });
            }

            if (createdNotifications.Count == 0)
                return (processed, succeeded, Math.Max(0, processed - succeeded), $"OpenChecks={openChecks.Count}, ExpiredRemoved={expiredIds.Count}, NotificationsCreated=0");

            await db.Notifications.AddRangeAsync(createdNotifications, ct);
            await db.AppointmentReminderDispatches.AddRangeAsync(createdDispatches, ct);
            await db.SaveChangesAsync(ct);
            succeeded += createdNotifications.Count;

            // Envia push via WebPush
            // Agrupa por idioma para mensagem correta (como no Reminder30Min).
            var ptUsers = createdNotifications.Where(n => n.Title == "Checkout pendente").Select(n => n.RecipientId).Distinct().ToList();
            var enUsers = createdNotifications.Where(n => n.Title == "Checkout pending").Select(n => n.RecipientId).Distinct().ToList();

            if (ptUsers.Count > 0)
            {
                await SendPushAsync(
                    pushSender,
                    createdNotifications.Where(n => n.Title == "Checkout pendente").ToList(),
                    "Checkout pendente",
                    "Você fez check-in, mas ainda não fez o check-out. Finalize o check-out agora.",
                    "Professional",
                    ptUsers);

                _logger.LogInformation("[CheckoutReminder] ENVIADO PT users=[{Users}] count={Count}", string.Join(",", ptUsers), ptUsers.Count);
            }

            if (enUsers.Count > 0)
            {
                await SendPushAsync(
                    pushSender,
                    createdNotifications.Where(n => n.Title == "Checkout pending").ToList(),
                    "Checkout pending",
                    "You checked in, but you haven't checked out yet. Please complete checkout now.",
                    "Professional",
                    enUsers);

                _logger.LogInformation("[CheckoutReminder] ENVIADO EN users=[{Users}] count={Count}", string.Join(",", enUsers), enUsers.Count);
            }

            var failed = Math.Max(0, processed - succeeded);
            return (processed, succeeded, failed, $"OpenChecks={openChecks.Count}, ExpiredRemoved={expiredIds.Count}, NotificationsCreated={createdNotifications.Count}");
        }

        private static bool IsPtBr(string? lang)
        {
            var l = (lang ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(l)) return true;
            return l.StartsWith("pt") || l == "pt-br" || l == "ptbr";
        }

        private static string BuildMessage(bool isPt, Appointment a)
        {
            var title = string.IsNullOrWhiteSpace(a.Title) ? (isPt ? "Seu agendamento" : "Your appointment") : a.Title;
            if (isPt)
            {
                if (!string.IsNullOrWhiteSpace(a.Address))
                    return $"{title}: você fez check-in e ainda não fez check-out. Endereço: {a.Address}."
                        + " Finalize o check-out agora.";
                return $"{title}: você fez check-in e ainda não fez check-out. Finalize o check-out agora.";
            }

            if (!string.IsNullOrWhiteSpace(a.Address))
                return $"{title}: you checked in and still haven't checked out. Address: {a.Address}. Please complete checkout now.";
            return $"{title}: you checked in and still haven't checked out. Please complete checkout now.";
        }

        private static async Task SendPushAsync(
            Services.IPushNotificationSender pushSender,
            List<Notification> created,
            string title,
            string message,
            string recipientRole,
            List<int> userIds)
        {
            var dto = new CreateNotificationDTO
            {
                Title = title,
                Message = message,
                Type = "Warning",
                RecipientRole = recipientRole,
                IsBroadcast = false,
                UserIds = userIds
            };

            await pushSender.TrySendForCreatedNotificationsAsync(created, dto);
        }
    }
}
