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

        public GpsTrackingRetentionHostedService(
            ILogger<GpsTrackingRetentionHostedService> logger,
            IServiceProvider sp,
            IOptions<GpsTrackingOptions> opts)
        {
            _logger = logger;
            _sp = sp;
            _opts = opts.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // roda 1x por dia
            var interval = TimeSpan.FromHours(24);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_opts.RetentionDays > 0)
                    {
                        using var scope = _sp.CreateScope();
                        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                        var threshold = DateTime.UtcNow.AddDays(-_opts.RetentionDays);
                        var deleted = await uow.GpsTrackings.DeleteOlderThanAsync(threshold);
                        if (deleted > 0)
                            _logger.LogInformation("GPS retention cleanup: deleted {Count} points older than {ThresholdUtc}", deleted, threshold);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GPS retention cleanup failed");
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
