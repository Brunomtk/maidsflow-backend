using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Limpeza diária de dados gerados por notificações automáticas (ex.: lembretes 30 min).
    /// - Remove registros antigos de AppointmentReminderDispatches (idempotência/log).
    /// - Remove notificações de lembrete antigas para não acumular histórico infinito.
    /// </summary>
    public class NotificationCleanupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationCleanupHostedService> _logger;
        private readonly IConfiguration _config;

        // Defaults (caso não exista config)
        private const int DefaultRunHour = 3; // 03:00
        private const int DefaultRunMinute = 0;
        private const int DefaultReminderRetentionDays = 7;
        private const int DefaultDispatchRetentionDays = 30;

        private static readonly string[] ReminderTitles =
        {
            "Agendamento em 30 minutos",
            "Appointment in 30 minutes"
        };

        public NotificationCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationCleanupHostedService> logger,
            IConfiguration config)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Loop diário: espera até o próximo horário configurado e roda a limpeza.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var tz = GetSaoPauloTimeZone();
                    var (runHour, runMinute) = GetRunTime();
                    var nextRunUtc = GetNextRunUtc(tz, runHour, runMinute);

                    var delay = nextRunUtc - DateTimeOffset.UtcNow;
                    if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

                    _logger.LogInformation("[AutoNotifications] Próxima limpeza agendada para {NextRunUtc} (UTC).", nextRunUtc);

                    await Task.Delay(delay, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await RunCleanupAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AutoNotifications] Erro no loop de limpeza diária. Tentando novamente em 5 minutos.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch { /* ignore */ }
                }
            }
        }

        private async Task RunCleanupAsync(CancellationToken ct)
        {
            var enabled = _config.GetValue("AutoNotifications:Cleanup:Enabled", true);
            if (!enabled)
            {
                _logger.LogInformation("[AutoNotifications] Limpeza diária desabilitada por configuração.");
                return;
            }

            var reminderRetentionDays = _config.GetValue("AutoNotifications:Cleanup:ReminderNotificationsRetentionDays", DefaultReminderRetentionDays);
            var dispatchRetentionDays = _config.GetValue("AutoNotifications:Cleanup:DispatchRetentionDays", DefaultDispatchRetentionDays);

            var nowUtc = DateTime.UtcNow;
            var notifCutoffUtc = nowUtc.AddDays(-Math.Max(1, reminderRetentionDays));
            var dispatchCutoffUtc = nowUtc.AddDays(-Math.Max(1, dispatchRetentionDays));

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();

            // 1) Limpar logs de dispatch antigos
            var dispatchesToDelete = await db.AppointmentReminderDispatches
                .Where(d => d.CreatedDate < dispatchCutoffUtc)
                .Select(d => d.Id)
                .ToListAsync(ct);

            if (dispatchesToDelete.Count > 0)
            {
                db.AppointmentReminderDispatches.RemoveRange(
                    db.AppointmentReminderDispatches.Where(d => dispatchesToDelete.Contains(d.Id))
                );
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("[AutoNotifications] Removidos {Count} dispatches antigos (cutoff {CutoffUtc}).", dispatchesToDelete.Count, dispatchCutoffUtc);
            }
            else
            {
                _logger.LogInformation("[AutoNotifications] Nenhum dispatch antigo para remover (cutoff {CutoffUtc}).", dispatchCutoffUtc);
            }

            // 2) Limpar notificações de lembrete antigas
            var remindersToDelete = await db.Notifications
                .Where(n => ReminderTitles.Contains(n.Title) && n.SentAt < notifCutoffUtc)
                .Select(n => n.Id)
                .ToListAsync(ct);

            if (remindersToDelete.Count > 0)
            {
                db.Notifications.RemoveRange(
                    db.Notifications.Where(n => remindersToDelete.Contains(n.Id))
                );
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("[AutoNotifications] Removidas {Count} notificações de lembrete antigas (cutoff {CutoffUtc}).", remindersToDelete.Count, notifCutoffUtc);
            }
            else
            {
                _logger.LogInformation("[AutoNotifications] Nenhuma notificação de lembrete antiga para remover (cutoff {CutoffUtc}).", notifCutoffUtc);
            }
        }

        private (int hour, int minute) GetRunTime()
        {
            var hour = _config.GetValue("AutoNotifications:Cleanup:RunAtLocalHour", DefaultRunHour);
            var minute = _config.GetValue("AutoNotifications:Cleanup:RunAtLocalMinute", DefaultRunMinute);

            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);
            return (hour, minute);
        }

        private static TimeZoneInfo GetSaoPauloTimeZone()
        {
            // Linux: "America/Sao_Paulo"
            // Windows: "E. South America Standard Time"
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
        }

        private static DateTimeOffset GetNextRunUtc(TimeZoneInfo tz, int localHour, int localMinute)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

            var nextLocal = new DateTimeOffset(
                nowLocal.Year, nowLocal.Month, nowLocal.Day,
                localHour, localMinute, 0,
                nowLocal.Offset
            );

            if (nextLocal <= nowLocal)
                nextLocal = nextLocal.AddDays(1);

            // DateTimeOffset já carrega o offset do horário local, então basta converter para UTC.
            return nextLocal.ToUniversalTime();
        }
    }
}
