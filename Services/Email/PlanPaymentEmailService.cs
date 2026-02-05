using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;

namespace Services.Email;

public class PlanPaymentEmailService : IPlanPaymentEmailService
{
    private readonly IUnitOfWork _uow;
    private readonly ISendGridEmailSender _sender;
    private readonly SendGridOptions _opt;

    public PlanPaymentEmailService(IUnitOfWork uow, ISendGridEmailSender sender, IOptions<SendGridOptions> opt)
    {
        _uow = uow;
        _sender = sender;
        _opt = opt.Value;
    }

    public async Task SendPlanPaymentSuccessAsync(
        int companyId,
        decimal amountPaid,
        string currency,
        string? invoiceNumber,
        string? hostedInvoiceUrl,
        string? invoicePdfUrl,
        long? periodStartUnix,
        long? periodEndUnix,
        long? paidAtUnix,
        CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetById(companyId);
        if (company == null) return;
        if (!company.ReceiveEmail) return;
        if (string.IsNullOrWhiteSpace(company.Email)) return;

        var planName = company.Plan?.Name ?? "Your plan";

        DateTime? periodStartUtc = periodStartUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(periodStartUnix.Value).UtcDateTime : null;
        DateTime? periodEndUtc = periodEndUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(periodEndUnix.Value).UtcDateTime : null;
        var paidAtUtc = paidAtUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(paidAtUnix.Value).UtcDateTime : DateTime.UtcNow;

        var model = new PlanPaymentSuccessEmailTemplate.Model(
            CompanyName: company.Name,
            PlanName: planName,
            AmountPaid: amountPaid,
            Currency: currency,
            PaidAtUtc: paidAtUtc,
            PeriodStartUtc: periodStartUtc,
            PeriodEndUtc: periodEndUtc,
            InvoiceNumber: invoiceNumber,
            HostedInvoiceUrl: hostedInvoiceUrl,
            InvoicePdfUrl: invoicePdfUrl,
            SupportUrl: _opt.SupportUrl
        );

        var (suffix, html, plain) = PlanPaymentSuccessEmailTemplate.Render(model);

        var subjectBase = string.IsNullOrWhiteSpace(_opt.PlanPaymentSuccessSubject)
            ? "Payment successful"
            : _opt.PlanPaymentSuccessSubject.Trim();

        var subject = string.IsNullOrWhiteSpace(suffix) ? subjectBase : $"{subjectBase} • {suffix}";

        // Our SendGrid sender reads FromEmail/FromName from SendGridOptions.
        // The message object only carries the recipient + content.
        var msg = new SendGridEmailMessage(
            ToEmail: company.Email,
            Subject: subject,
            PlainText: plain,
            Html: html,
            ToName: company.Responsible
        );

        await _sender.SendAsync(msg, ct);
    }
}
