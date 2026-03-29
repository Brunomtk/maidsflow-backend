using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Enums.Notifications;
using Core.Enums.Payment;
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
    public class PaymentDueReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentDueReminderHostedService> _logger;
        private readonly IConfiguration _config;
        private readonly Services.IBackgroundJobMonitorService _jobMonitor;

        private const int DefaultRunIntervalMinutes = 60;

        public PaymentDueReminderHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<PaymentDueReminderHostedService> logger,
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

            var enabled = _config.GetValue("AutoNotifications:Payments:Enabled", true);
            if (!enabled)
            {
                await _jobMonitor.MarkDisabledAsync(
                    Services.BackgroundJobKeys.PaymentDueReminder,
                    "Payment Due Reminder",
                    "Finance",
                    "Disabled by configuration.",
                    null,
                    stoppingToken);
                return;
            }

            var intervalMinutes = _config.GetValue<int?>("AutoNotifications:Payments:RunIntervalMinutes") ?? DefaultRunIntervalMinutes;
            if (intervalMinutes <= 0)
                intervalMinutes = DefaultRunIntervalMinutes;

            var interval = TimeSpan.FromMinutes(intervalMinutes);
            await _jobMonitor.UpdateHeartbeatAsync(Services.BackgroundJobKeys.PaymentDueReminder, DateTime.UtcNow.Add(interval), true, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(interval);
                var run = await _jobMonitor.MarkStartedAsync(
                    Services.BackgroundJobKeys.PaymentDueReminder,
                    "Payment Due Reminder",
                    "Finance",
                    nextRunUtc,
                    stoppingToken);

                try
                {
                    var result = await RunAsync(stoppingToken);
                    await _jobMonitor.MarkSucceededAsync(run, result.summary, result.processed, result.succeeded, result.failed, nextRunUtc, stoppingToken);
                    await Task.Delay(interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Payments] Error while processing due reminders.");
                    await _jobMonitor.MarkFailedAsync(run, ex, "Payment due reminder failed.", nextPlannedRunAtUtc: DateTime.UtcNow.AddMinutes(5), ct: stoppingToken);
                    try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); } catch { }
                }
            }
        }

        private async Task<(int processed, int succeeded, int failed, string summary)> RunAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();

            var today = DateTime.UtcNow.Date;
            var now = DateTime.UtcNow;
            var processed = 0;
            var succeeded = 0;
            var failed = 0;

            var payments = await db.Payments
                .AsTracking()
                .Where(p => p.CompanyId > 0
                    && p.Status != PaymentStatus.Paid
                    && p.Status != PaymentStatus.Cancelled)
                .ToListAsync(ct);

            foreach (var payment in payments)
            {
                try
                {
                    processed++;
                    var dueDate = payment.DueDate.Date;
                    var dueKind = payment.FinancialType == PaymentFinancialType.Expense ? "Accounts payable" : "Accounts receivable";

                    if (dueDate < today)
                    {
                        if (payment.Status == PaymentStatus.Pending)
                        {
                            payment.Status = PaymentStatus.Overdue;
                            payment.UpdatedDate = now;
                        }

                        var overdueToken = BuildAlertToken(payment.Id, "overdue", dueDate);
                        var hasOverdue = await db.Notifications
                            .AnyAsync(n => n.CompanyId == payment.CompanyId && n.Message.Contains(overdueToken), ct);

                        if (!hasOverdue)
                        {
                            db.Notifications.Add(new Notification
                            {
                                Title = $"{dueKind} overdue",
                                Message = $"The {dueKind.ToLowerInvariant()} {BuildReference(payment)} became overdue on {dueDate:MM/dd/yyyy}. Category: {BuildCategory(payment)}. Amount: {payment.Amount:0.00}. {overdueToken}",
                                Type = NotificationType.Warning,
                                RecipientId = 0,
                                RecipientRole = UserRole.Company,
                                CompanyId = payment.CompanyId,
                                Status = NotificationStatus.Unread,
                                SentAt = now,
                                CreatedDate = now,
                                UpdatedDate = now
                            });
                        }
                    }
                    else if (dueDate == today)
                    {
                        var dueTodayToken = BuildAlertToken(payment.Id, "due-today", dueDate);
                        var hasDueToday = await db.Notifications
                            .AnyAsync(n => n.CompanyId == payment.CompanyId && n.Message.Contains(dueTodayToken), ct);

                        if (!hasDueToday)
                        {
                            db.Notifications.Add(new Notification
                            {
                                Title = $"{dueKind} due today",
                                Message = $"The {dueKind.ToLowerInvariant()} {BuildReference(payment)} is due today ({dueDate:MM/dd/yyyy}). Category: {BuildCategory(payment)}. Amount: {payment.Amount:0.00}. {dueTodayToken}",
                                Type = NotificationType.Info,
                                RecipientId = 0,
                                RecipientRole = UserRole.Company,
                                CompanyId = payment.CompanyId,
                                Status = NotificationStatus.Unread,
                                SentAt = now,
                                CreatedDate = now,
                                UpdatedDate = now
                            });
                        }
                    }

                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "[Payments] Failed to process payment {PaymentId} for company {CompanyId}.", payment.Id, payment.CompanyId);
                }
            }

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(ct);

            var summary = $"Processed {processed} payment(s); {succeeded} succeeded; {failed} failed.";
            return (processed, succeeded, failed, summary);
        }

        private static string BuildAlertToken(int paymentId, string kind, DateTime date) => $"[PAYMENT_ALERT:{kind}:{paymentId}:{date:yyyyMMdd}]";
        private static string BuildReference(Payment payment) => string.IsNullOrWhiteSpace(payment.Reference) ? $"entry #{payment.Id}" : payment.Reference;
        private static string BuildCategory(Payment payment) => string.IsNullOrWhiteSpace(payment.PaymentCategoryName) ? "Uncategorized" : payment.PaymentCategoryName!;
    }
}
