using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.Reports;
using Core.Options;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services;
using Services.Email;

namespace ControlApi.BackgroundJobs;

public sealed class MonthlyCompanyReportEmailHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonthlyCompanyReportEmailHostedService> _logger;
    private readonly IBackgroundJobMonitorService _jobMonitor;
    private readonly MonthlyReportEmailOptions _options;

    public MonthlyCompanyReportEmailHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<MonthlyCompanyReportEmailHostedService> logger,
        IBackgroundJobMonitorService jobMonitor,
        IOptions<MonthlyReportEmailOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _jobMonitor = jobMonitor;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await _jobMonitor.EnsureDefaultsRegisteredAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRunUtc = DateTime.UtcNow.AddMinutes(Math.Max(10, _options.PollIntervalMinutes));

            if (!_options.Enabled)
            {
                await _jobMonitor.MarkDisabledAsync(BackgroundJobKeys.MonthlyCompanyReportEmail, "Monthly Company Report Email", "Reports", "Disabled by configuration.", nextRunUtc, stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(10, _options.PollIntervalMinutes)), stoppingToken);
                continue;
            }

            var run = await _jobMonitor.MarkStartedAsync(BackgroundJobKeys.MonthlyCompanyReportEmail, "Monthly Company Report Email", "Reports", nextRunUtc, stoppingToken);
            try
            {
                var result = await RunOnceAsync(stoppingToken);
                await _jobMonitor.MarkSucceededAsync(run, result.summary, result.processed, result.succeeded, result.failed, nextRunUtc, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MonthlyReportEmail] Unexpected error while sending monthly company report emails.");
                await _jobMonitor.MarkFailedAsync(run, ex, "Unexpected error in monthly company report email job.", nextPlannedRunAtUtc: nextRunUtc, ct: stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(10, _options.PollIntervalMinutes)), stoppingToken);
        }
    }

    private async Task<(int processed, int succeeded, int failed, string summary)> RunOnceAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc.Day != Math.Clamp(_options.RunOnDayOfMonth, 1, 28) || nowUtc.Hour < Math.Clamp(_options.RunHourUtc, 0, 23))
        {
            return (0, 0, 0, "Waiting for the configured monthly schedule window.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContextClass>();
        var emailService = scope.ServiceProvider.GetRequiredService<ICompanyReportEmailService>();

        var firstDayCurrentMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1);
        var firstDayPreviousMonth = firstDayCurrentMonth.AddMonths(-1);
        var lastDayPreviousMonth = firstDayCurrentMonth.AddDays(-1);
        var periodRequest = new SendCompanyReportEmailRequestDto
        {
            StartDate = firstDayPreviousMonth,
            EndDate = lastDayPreviousMonth,
            UsePreviousMonthByDefault = true,
        };

        var companies = await db.Companies.AsNoTracking()
            .Where(x => x.Status == Core.Enums.StatusEnum.Active && x.ReceiveEmail && !string.IsNullOrWhiteSpace(x.Email))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;

        foreach (var company in companies)
        {
            processed++;
            var dispatchKey = $"monthly:{company.Id}:{company.Email.Trim().ToLowerInvariant()}:{firstDayPreviousMonth:yyyyMM}";
            var alreadySent = await db.CompanyReportEmailDispatches.AsNoTracking().AnyAsync(x => x.DispatchKey == dispatchKey, ct);
            if (alreadySent)
                continue;

            try
            {
                periodRequest.RecipientEmail = company.Email;
                await emailService.SendAsync(company.Id, periodRequest, _options.TriggeredByValue, ct);
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "[MonthlyReportEmail] Failed to send scheduled report email for companyId={CompanyId}", company.Id);
            }
        }

        return (processed, succeeded, failed, $"Processed {processed} companies. Sent {succeeded} monthly report emails.");
    }
}
