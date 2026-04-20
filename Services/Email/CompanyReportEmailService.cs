using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DTO.Reports;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Services.Integrations.SendGrid;
using Services.Security;

namespace Services.Email;

public sealed class CompanyReportEmailService : ICompanyReportEmailService
{
    private readonly DbContextClass _db;
    private readonly IReportsService _reportsService;
    private readonly ISendGridEmailSender _emailSender;
    private readonly SendGridOptions _sendGridOptions;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeGuard _scopeGuard;
    private readonly ILogger<CompanyReportEmailService> _logger;

    public CompanyReportEmailService(
        DbContextClass db,
        IReportsService reportsService,
        ISendGridEmailSender emailSender,
        IOptions<SendGridOptions> sendGridOptions,
        ICurrentUser currentUser,
        IScopeGuard scopeGuard,
        ILogger<CompanyReportEmailService> logger)
    {
        _db = db;
        _reportsService = reportsService;
        _emailSender = emailSender;
        _sendGridOptions = sendGridOptions.Value;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _logger = logger;
    }

    public async Task<SendCompanyReportEmailResultDto> SendAsync(int companyId, SendCompanyReportEmailRequestDto request, string triggeredBy, CancellationToken ct = default)
    {
        if (companyId <= 0)
            throw new InvalidOperationException("A valid company id is required.");

        if (string.Equals(triggeredBy, "manual", StringComparison.OrdinalIgnoreCase))
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new UnauthorizedAccessException("You do not have permission to send report emails.");

            if (_currentUser.IsCompany)
            {
                if (!_currentUser.CompanyId.HasValue || _currentUser.CompanyId.Value != companyId)
                    throw new UnauthorizedAccessException("You do not have permission to send report emails for this company.");

                await _scopeGuard.EnsureCompanyAccessAsync(companyId);
            }
        }

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found.");

        var period = ResolvePeriod(request);
        var report = await _reportsService.GetCompanyReportByCompanyIdAsync(companyId, new ReportQueryDto
        {
            StartDate = period.startDate,
            EndDate = period.endDate,
            ProfessionalId = request.StartDate.HasValue || request.EndDate.HasValue ? null : null,
            Page = 1,
            PageSize = 20,
        });

        var recipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail)
            ? company.Email?.Trim()
            : request.RecipientEmail.Trim();

        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new InvalidOperationException("The company does not have a valid recipient email configured.");

        var subject = $"Monthly performance report · {company.Name} · {period.startDate:MMMM yyyy}";
        var rendered = CompanyMonthlyReportEmailTemplate.Render(new CompanyMonthlyReportEmailTemplate.Model(
            CompanyName: company.Name,
            RecipientEmail: recipientEmail,
            PeriodLabel: $"{period.startDate:MMM dd, yyyy} - {period.endDate:MMM dd, yyyy}",
            GeneratedAtUtc: DateTime.UtcNow,
            ExecutiveNarrative: report.ExecutiveSummary.Narrative,
            HealthStatus: report.ExecutiveSummary.HealthStatus,
            OverviewCards: report.OverviewCards,
            FinancialCards: report.Financial.Cards,
            OperationsCards: report.Operations.Cards,
            TeamCards: report.Team.Cards,
            CustomerCards: report.Customers.Cards,
            Strengths: report.ExecutiveSummary.Strengths.Take(4).ToArray(),
            Risks: report.ExecutiveSummary.Risks.Take(4).ToArray(),
            RecommendedActions: report.ExecutiveSummary.RecommendedActions.Take(4).ToArray(),
            SupportUrl: string.IsNullOrWhiteSpace(_sendGridOptions.SupportUrl) ? null : _sendGridOptions.SupportUrl.Trim()
        ), subject);

        var send = await _emailSender.SendAsync(new SendGridEmailMessage(
            ToEmail: recipientEmail,
            Subject: rendered.Subject,
            PlainText: rendered.PlainText,
            Html: rendered.Html,
            ToName: company.Responsible
        ), ct);

        if (!send.Ok)
        {
            var details = string.IsNullOrWhiteSpace(send.ResponseBody) ? send.Error : send.ResponseBody;
            throw new InvalidOperationException($"Failed to send report email. StatusCode={send.StatusCode}. Details={details}");
        }

        try
        {
            _db.CompanyReportEmailDispatches.Add(new Core.Models.CompanyReportEmailDispatch
            {
                CompanyId = companyId,
                RecipientEmail = recipientEmail,
                PeriodStartDate = period.startDate,
                PeriodEndDate = period.endDate,
                Subject = rendered.Subject,
                SentAtUtc = DateTime.UtcNow,
                TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "manual" : triggeredBy.Trim(),
                DispatchKey = string.Equals(triggeredBy, "system-monthly", StringComparison.OrdinalIgnoreCase)
                    ? $"monthly:{companyId}:{recipientEmail.Trim().ToLowerInvariant()}:{period.startDate:yyyyMM}"
                    : null,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Company report email was sent, but the dispatch audit record could not be persisted for companyId={CompanyId}.", companyId);
            _db.ChangeTracker.Clear();
        }

        return new SendCompanyReportEmailResultDto
        {
            Success = true,
            CompanyId = companyId,
            CompanyName = company.Name,
            RecipientEmail = recipientEmail,
            StartDate = period.startDate,
            EndDate = period.endDate,
            Subject = rendered.Subject,
            SentAtUtc = DateTime.UtcNow,
        };
    }

    public static (DateTime startDate, DateTime endDate) ResolvePeriod(SendCompanyReportEmailRequestDto request)
    {
        if (request.StartDate.HasValue && request.EndDate.HasValue)
            return (request.StartDate.Value.Date, request.EndDate.Value.Date);

        if (request.UsePreviousMonthByDefault || !request.StartDate.HasValue || !request.EndDate.HasValue)
        {
            var now = DateTime.UtcNow;
            var firstDayCurrentMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayPreviousMonth = firstDayCurrentMonth.AddMonths(-1);
            var lastDayPreviousMonth = firstDayCurrentMonth.AddDays(-1);
            return (firstDayPreviousMonth, lastDayPreviousMonth);
        }

        return (request.StartDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30), request.EndDate?.Date ?? DateTime.UtcNow.Date);
    }
}
