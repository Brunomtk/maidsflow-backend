using Core.Enums.Messaging;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;
using Services.Messaging;

namespace ControlApi.BackgroundJobs
{
    /// <summary>
    /// Polls every 2 minutes for SMS logs in <c>Failed</c> state whose <c>ScheduledForUtc</c> has elapsed,
    /// and re-attempts them via <see cref="ISmsDispatchService"/>.
    ///
    /// Retry policy is enforced inside the dispatch service (max attempts + exponential backoff).
    /// This hosted service is just the scheduler that wakes up logs ready to be retried.
    /// </summary>
    public class SmsRetryHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmsRetryHostedService> _logger;
        private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromMinutes(2);
        private const int BATCH_LIMIT = 50;

        private readonly IBackgroundJobMonitorService _jobMonitor;

        public SmsRetryHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<SmsRetryHostedService> logger,
            IBackgroundJobMonitorService jobMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _jobMonitor = jobMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SmsRetryHostedService started; polling every {Interval}", POLL_INTERVAL);
            await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(POLL_INTERVAL);
                var run = await _jobMonitor.MarkStartedAsync(
                    BackgroundJobKeys.SmsRetry, "SMS Retry Worker", "Messaging", nextRunUtc, stoppingToken);
                try
                {
                    var (retried, sent, blocked, stillFailed, terminal) = await ProcessOnceAsync(stoppingToken);
                    var summary = $"retried:{retried} sent:{sent} blocked:{blocked} willRetry:{stillFailed} terminal:{terminal}";
                    await _jobMonitor.MarkSucceededAsync(run, summary, retried, sent, terminal, nextRunUtc, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SmsRetryHostedService loop error");
                    await _jobMonitor.MarkFailedAsync(run, ex, "Unexpected error in SMS retry job.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                }
                try { await Task.Delay(POLL_INTERVAL, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task<(int retried, int sent, int blocked, int stillFailed, int terminal)> ProcessOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
            var dispatch = scope.ServiceProvider.GetRequiredService<ISmsDispatchService>();

            var now = DateTime.UtcNow;
            // Pick failed logs that:
            //  - are SMS channel
            //  - were NOT blocked by compliance (those need admin/company to fix policy first)
            //  - have a scheduled retry slot in the past
            //  - haven't reached max attempts (the dispatcher checks; we filter here for performance)
            var due = await db.AppointmentMessageLogs
                .Where(l =>
                    l.Channel == AppointmentMessageChannel.Sms &&
                    l.Status == AppointmentMessageStatus.Failed &&
                    !l.WasBlockedByMessagingPolicy &&
                    l.ScheduledForUtc != null &&
                    l.ScheduledForUtc <= now &&
                    l.Attempt < 5)
                .OrderBy(l => l.ScheduledForUtc)
                .Take(BATCH_LIMIT)
                .Select(l => l.Id)
                .ToListAsync(ct);

            if (due.Count == 0) return (0, 0, 0, 0, 0);

            _logger.LogInformation("SmsRetryHostedService: retrying {Count} failed SMS logs", due.Count);

            int sent = 0, blocked = 0, stillFailed = 0, terminal = 0;
            foreach (var id in due)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var result = await dispatch.RetryAsync(id, ct);
                    switch (result.Outcome)
                    {
                        case SmsDispatchOutcome.Sent: sent++; break;
                        case SmsDispatchOutcome.Blocked: blocked++; break;
                        case SmsDispatchOutcome.FailedWillRetry: stillFailed++; break;
                        case SmsDispatchOutcome.FailedTerminal: terminal++; break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SmsRetryHostedService failed to retry log {LogId}", id);
                }
            }

            _logger.LogInformation(
                "SmsRetryHostedService batch result — sent:{Sent} blocked:{Blocked} willRetry:{WillRetry} terminal:{Terminal}",
                sent, blocked, stillFailed, terminal);
            return (due.Count, sent, blocked, stillFailed, terminal);
        }
    }
}
