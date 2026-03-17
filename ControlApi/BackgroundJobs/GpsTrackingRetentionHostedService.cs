using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Options;
using Infrastructure.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ControlApi.BackgroundJobs
{
    public class GpsTrackingRetentionHostedService : BackgroundService
    {
        private readonly ILogger<GpsTrackingRetentionHostedService> _logger;
        private readonly IServiceProvider _sp;
        private readonly GpsTrackingOptions _opts;
        private readonly Services.IBackgroundJobMonitorService _jobMonitor;

        public GpsTrackingRetentionHostedService(
            ILogger<GpsTrackingRetentionHostedService> logger,
            IServiceProvider sp,
            IOptions<GpsTrackingOptions> opts,
            Services.IBackgroundJobMonitorService jobMonitor)
        {
            _logger = logger;
            _sp = sp;
            _opts = opts.Value;
            _jobMonitor = jobMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);
            // roda 1x por dia
            var interval = TimeSpan.FromHours(24);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRunUtc = DateTime.UtcNow.Add(interval);
                if (_opts.RetentionDays <= 0)
                {
                    await _jobMonitor.MarkDisabledAsync(Services.BackgroundJobKeys.GpsTrackingRetention, "GPS Tracking Retention", "Maintenance", "RetentionDays <= 0.", nextRunUtc, stoppingToken);
                }
                else
                {
                    var run = await _jobMonitor.MarkStartedAsync(Services.BackgroundJobKeys.GpsTrackingRetention, "GPS Tracking Retention", "Maintenance", nextRunUtc, stoppingToken);
                    try
                    {
                        using var scope = _sp.CreateScope();
                        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                        var threshold = DateTime.UtcNow.AddDays(-_opts.RetentionDays);
                        var deleted = await uow.GpsTrackings.DeleteOlderThanAsync(threshold);
                        if (deleted > 0)
                            _logger.LogInformation("GPS retention cleanup: deleted {Count} points older than {ThresholdUtc}", deleted, threshold);

                        await _jobMonitor.MarkSucceededAsync(run, $"DeletedPoints={deleted}, RetentionDays={_opts.RetentionDays}", deleted, deleted, 0, nextRunUtc, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "GPS retention cleanup failed");
                        await _jobMonitor.MarkFailedAsync(run, ex, "GPS retention cleanup failed.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
                    }
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch
                {
                    // ignore cancellation
                }
            }
        }
    }
}
