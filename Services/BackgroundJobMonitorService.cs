using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.BackgroundJobs;
using Core.Enums.BackgroundJobs;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Services
{
    public static class BackgroundJobKeys
    {
        public const string AppointmentReminder = "appointment-reminder";
        public const string CheckoutReminder = "checkout-reminder";
        public const string ReviewRequest = "review-request";
        public const string NotificationCleanup = "notification-cleanup";
        public const string GpsTrackingRetention = "gps-tracking-retention";
        public const string PaymentDueReminder = "payment-due-reminder";

        public static readonly IReadOnlyList<(string Key, string DisplayName, string Category)> Defaults =
            new List<(string, string, string)>
            {
                (AppointmentReminder, "Appointment Reminder", "Notifications"),
                (CheckoutReminder, "Checkout Reminder", "Notifications"),
                (ReviewRequest, "Review Request", "Reviews"),
                (NotificationCleanup, "Notification Cleanup", "Maintenance"),
                (GpsTrackingRetention, "GPS Tracking Retention", "Maintenance"),
                (PaymentDueReminder, "Payment Due Reminder", "Finance")
            };
    }

    public sealed class BackgroundJobRunContext
    {
        internal BackgroundJobRunContext(string jobKey, int executionId, Stopwatch stopwatch)
        {
            JobKey = jobKey;
            ExecutionId = executionId;
            Stopwatch = stopwatch;
        }

        public string JobKey { get; }
        public int ExecutionId { get; }
        internal Stopwatch Stopwatch { get; }
    }

    public interface IBackgroundJobMonitorService
    {
        Task EnsureDefaultsRegisteredAsync(CancellationToken ct = default);
        Task UpdateHeartbeatAsync(string jobKey, DateTime? nextPlannedRunAtUtc, bool isEnabled, CancellationToken ct = default);
        Task<BackgroundJobRunContext> MarkStartedAsync(string jobKey, string displayName, string? category, DateTime? nextPlannedRunAtUtc, CancellationToken ct = default);
        Task MarkSucceededAsync(BackgroundJobRunContext context, string? summary, int? itemsProcessed = null, int? itemsSucceeded = null, int? itemsFailed = null, DateTime? nextPlannedRunAtUtc = null, CancellationToken ct = default);
        Task MarkFailedAsync(BackgroundJobRunContext context, Exception exception, string? summary = null, int? itemsProcessed = null, int? itemsSucceeded = null, int? itemsFailed = null, DateTime? nextPlannedRunAtUtc = null, CancellationToken ct = default);
        Task MarkDisabledAsync(string jobKey, string displayName, string? category, string? summary, DateTime? nextPlannedRunAtUtc = null, CancellationToken ct = default);
        Task<IReadOnlyList<BackgroundJobStatusDTO>> GetStatusesAsync(CancellationToken ct = default);
        Task<BackgroundJobStatusDTO?> GetStatusByKeyAsync(string jobKey, CancellationToken ct = default);
        Task<IReadOnlyList<BackgroundJobExecutionDTO>> GetExecutionsAsync(string jobKey, int page, int pageSize, CancellationToken ct = default);
    }

    public class BackgroundJobMonitorService : IBackgroundJobMonitorService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public BackgroundJobMonitorService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task EnsureDefaultsRegisteredAsync(CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                foreach (var item in BackgroundJobKeys.Defaults)
                    await EnsureStatusEntityAsync(db, item.Key, item.DisplayName, item.Category, ct);

                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Fail-open: background jobs must not crash the host if monitoring tables are unavailable.
            }
        }

        public async Task UpdateHeartbeatAsync(string jobKey, DateTime? nextPlannedRunAtUtc, bool isEnabled, CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                var meta = BackgroundJobKeys.Defaults.FirstOrDefault(x => x.Key == jobKey);
                var status = await EnsureStatusEntityAsync(db, jobKey, string.IsNullOrWhiteSpace(meta.DisplayName) ? jobKey : meta.DisplayName, meta.Category, ct);
                status.IsEnabled = isEnabled;
                status.NextPlannedRunAtUtc = nextPlannedRunAtUtc;
                status.LastHeartbeatAtUtc = DateTime.UtcNow;
                if (!isEnabled)
                    status.CurrentStatus = BackgroundJobRunStatus.Disabled;
                status.UpdatedDate = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Ignore monitoring persistence failures.
            }
        }

        public async Task<BackgroundJobRunContext> MarkStartedAsync(string jobKey, string displayName, string? category, DateTime? nextPlannedRunAtUtc, CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                var now = DateTime.UtcNow;
                var status = await EnsureStatusEntityAsync(db, jobKey, displayName, category, ct);
                status.DisplayName = displayName;
                status.Category = category;
                status.IsEnabled = true;
                status.CurrentStatus = BackgroundJobRunStatus.Running;
                status.LastStartedAtUtc = now;
                status.LastHeartbeatAtUtc = now;
                status.LastError = null;
                status.NextPlannedRunAtUtc = nextPlannedRunAtUtc;
                status.UpdatedDate = now;

                var exec = new BackgroundJobExecution
                {
                    JobKey = jobKey,
                    Status = BackgroundJobRunStatus.Running,
                    StartedAtUtc = now,
                    TriggeredBy = "system",
                    CreatedDate = now,
                    UpdatedDate = now
                };

                db.BackgroundJobExecutions.Add(exec);
                await db.SaveChangesAsync(ct);
                await PruneExecutionsAsync(db, jobKey, 3, ct);
                await db.SaveChangesAsync(ct);
                return new BackgroundJobRunContext(jobKey, exec.Id, Stopwatch.StartNew());
            }
            catch
            {
                return new BackgroundJobRunContext(jobKey, 0, Stopwatch.StartNew());
            }
        }

        public async Task MarkSucceededAsync(BackgroundJobRunContext context, string? summary, int? itemsProcessed = null, int? itemsSucceeded = null, int? itemsFailed = null, DateTime? nextPlannedRunAtUtc = null, CancellationToken ct = default)
        {
            context.Stopwatch.Stop();
            if (context.ExecutionId <= 0)
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                var now = DateTime.UtcNow;
                var status = await db.BackgroundJobStatuses.FirstAsync(x => x.JobKey == context.JobKey, ct);
                var exec = await db.BackgroundJobExecutions.FirstAsync(x => x.Id == context.ExecutionId, ct);

                exec.Status = BackgroundJobRunStatus.Success;
                exec.FinishedAtUtc = now;
                exec.DurationMs = context.Stopwatch.ElapsedMilliseconds;
                exec.Summary = summary;
                exec.ItemsProcessed = itemsProcessed;
                exec.ItemsSucceeded = itemsSucceeded;
                exec.ItemsFailed = itemsFailed;
                exec.UpdatedDate = now;

                status.CurrentStatus = BackgroundJobRunStatus.Success;
                status.LastFinishedAtUtc = now;
                status.LastSuccessAtUtc = now;
                status.LastDurationMs = context.Stopwatch.ElapsedMilliseconds;
                status.LastSummary = summary;
                status.LastError = null;
                status.ConsecutiveFailures = 0;
                status.LastHeartbeatAtUtc = now;
                status.NextPlannedRunAtUtc = nextPlannedRunAtUtc;
                status.UpdatedDate = now;

                await PruneExecutionsAsync(db, context.JobKey, 3, ct);
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Ignore monitoring persistence failures.
            }
        }

        public async Task MarkFailedAsync(BackgroundJobRunContext context, Exception exception, string? summary = null, int? itemsProcessed = null, int? itemsSucceeded = null, int? itemsFailed = null, DateTime? nextPlannedRunAtUtc = null, CancellationToken ct = default)
        {
            context.Stopwatch.Stop();
            if (context.ExecutionId <= 0)
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                var now = DateTime.UtcNow;
                var status = await db.BackgroundJobStatuses.FirstAsync(x => x.JobKey == context.JobKey, ct);
                var exec = await db.BackgroundJobExecutions.FirstAsync(x => x.Id == context.ExecutionId, ct);

                exec.Status = BackgroundJobRunStatus.Failed;
                exec.FinishedAtUtc = now;
                exec.DurationMs = context.Stopwatch.ElapsedMilliseconds;
                exec.Summary = summary;
                exec.Error = exception.Message;
                exec.ItemsProcessed = itemsProcessed;
                exec.ItemsSucceeded = itemsSucceeded;
                exec.ItemsFailed = itemsFailed;
                exec.UpdatedDate = now;

                status.CurrentStatus = BackgroundJobRunStatus.Failed;
                status.LastFinishedAtUtc = now;
                status.LastFailureAtUtc = now;
                status.LastDurationMs = context.Stopwatch.ElapsedMilliseconds;
                status.LastSummary = summary;
                status.LastError = exception.Message;
                status.ConsecutiveFailures += 1;
                status.LastHeartbeatAtUtc = now;
                status.NextPlannedRunAtUtc = nextPlannedRunAtUtc;
                status.UpdatedDate = now;

                await PruneExecutionsAsync(db, context.JobKey, 3, ct);
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Ignore monitoring persistence failures.
            }
        }

        public async Task MarkDisabledAsync(string jobKey, string displayName, string? category, string? summary, DateTime? nextPlannedRunAtUtc = null, CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                var now = DateTime.UtcNow;
                var status = await EnsureStatusEntityAsync(db, jobKey, displayName, category, ct);
                status.IsEnabled = false;
                status.CurrentStatus = BackgroundJobRunStatus.Disabled;
                status.LastSummary = summary;
                status.LastHeartbeatAtUtc = now;
                status.NextPlannedRunAtUtc = nextPlannedRunAtUtc;
                status.UpdatedDate = now;
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Ignore monitoring persistence failures.
            }
        }

        public async Task<IReadOnlyList<BackgroundJobStatusDTO>> GetStatusesAsync(CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                await EnsureDefaultsRegisteredAsync(ct);
                return await db.BackgroundJobStatuses.AsNoTracking()
                    .OrderBy(x => x.DisplayName)
                    .Select(x => new BackgroundJobStatusDTO
                    {
                        JobKey = x.JobKey,
                        DisplayName = x.DisplayName,
                        Category = x.Category,
                        IsEnabled = x.IsEnabled,
                        CurrentStatus = x.CurrentStatus,
                        LastStartedAtUtc = x.LastStartedAtUtc,
                        LastFinishedAtUtc = x.LastFinishedAtUtc,
                        LastSuccessAtUtc = x.LastSuccessAtUtc,
                        LastFailureAtUtc = x.LastFailureAtUtc,
                        LastHeartbeatAtUtc = x.LastHeartbeatAtUtc,
                        LastDurationMs = x.LastDurationMs,
                        LastError = x.LastError,
                        LastSummary = x.LastSummary,
                        ConsecutiveFailures = x.ConsecutiveFailures,
                        NextPlannedRunAtUtc = x.NextPlannedRunAtUtc
                    })
                    .ToListAsync(ct);
            }
            catch
            {
                return Array.Empty<BackgroundJobStatusDTO>();
            }
        }

        public async Task<BackgroundJobStatusDTO?> GetStatusByKeyAsync(string jobKey, CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                await EnsureDefaultsRegisteredAsync(ct);
                return await db.BackgroundJobStatuses.AsNoTracking()
                    .Where(x => x.JobKey == jobKey)
                    .Select(x => new BackgroundJobStatusDTO
                    {
                        JobKey = x.JobKey,
                        DisplayName = x.DisplayName,
                        Category = x.Category,
                        IsEnabled = x.IsEnabled,
                        CurrentStatus = x.CurrentStatus,
                        LastStartedAtUtc = x.LastStartedAtUtc,
                        LastFinishedAtUtc = x.LastFinishedAtUtc,
                        LastSuccessAtUtc = x.LastSuccessAtUtc,
                        LastFailureAtUtc = x.LastFailureAtUtc,
                        LastHeartbeatAtUtc = x.LastHeartbeatAtUtc,
                        LastDurationMs = x.LastDurationMs,
                        LastError = x.LastError,
                        LastSummary = x.LastSummary,
                        ConsecutiveFailures = x.ConsecutiveFailures,
                        NextPlannedRunAtUtc = x.NextPlannedRunAtUtc
                    })
                    .FirstOrDefaultAsync(ct);
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<BackgroundJobExecutionDTO>> GetExecutionsAsync(string jobKey, int page, int pageSize, CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 3;
                if (pageSize > 5) pageSize = 3;

                return await db.BackgroundJobExecutions.AsNoTracking()
                    .Where(x => x.JobKey == jobKey)
                    .OrderByDescending(x => x.StartedAtUtc)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new BackgroundJobExecutionDTO
                    {
                        Id = x.Id,
                        JobKey = x.JobKey,
                        Status = x.Status,
                        StartedAtUtc = x.StartedAtUtc,
                        FinishedAtUtc = x.FinishedAtUtc,
                        DurationMs = x.DurationMs,
                        Summary = x.Summary,
                        Error = x.Error,
                        ItemsProcessed = x.ItemsProcessed,
                        ItemsSucceeded = x.ItemsSucceeded,
                        ItemsFailed = x.ItemsFailed,
                        TriggeredBy = x.TriggeredBy
                    })
                    .ToListAsync(ct);
            }
            catch
            {
                return Array.Empty<BackgroundJobExecutionDTO>();
            }
        }


        private static async Task PruneExecutionsAsync(DbContextClass db, string jobKey, int keepLast, CancellationToken ct)
        {
            if (keepLast < 1)
                keepLast = 1;

            var executionsToDelete = await db.BackgroundJobExecutions
                .Where(x => x.JobKey == jobKey)
                .OrderByDescending(x => x.StartedAtUtc)
                .ThenByDescending(x => x.Id)
                .Skip(keepLast)
                .ToListAsync(ct);

            if (executionsToDelete.Count == 0)
                return;

            db.BackgroundJobExecutions.RemoveRange(executionsToDelete);
        }

        private async Task<BackgroundJobStatus> EnsureStatusEntityAsync(DbContextClass db, string jobKey, string displayName, string? category, CancellationToken ct)
        {
            var entity = await db.BackgroundJobStatuses.FirstOrDefaultAsync(x => x.JobKey == jobKey, ct);
            if (entity != null)
                return entity;

            var now = DateTime.UtcNow;
            entity = new BackgroundJobStatus
            {
                JobKey = jobKey,
                DisplayName = displayName,
                Category = category,
                CreatedDate = now,
                UpdatedDate = now,
                CurrentStatus = BackgroundJobRunStatus.Idle,
                IsEnabled = true
            };
            db.BackgroundJobStatuses.Add(entity);
            return entity;
        }
    }
}
