using System;
using System.Globalization;
using System.Text;
using Services.Localization;

namespace Services.Integrations.SendGrid;

public static class PlanPaymentFailedEmailTemplate
{
    public sealed record Model(
        string CompanyName,
        string PlanName,
        decimal AmountDue,
        string Currency,
        DateTime FailedAtUtc,
        DateTime? PeriodStartUtc,
        DateTime? PeriodEndUtc,
        DateTime? NextPaymentAttemptUtc,
        string? InvoiceNumber,
        string? HostedInvoiceUrl,
        string? InvoicePdfUrl,
        string? SupportUrl
    );

    public static (string SubjectSuffix, string Html, string PlainText) Render(Model m, IMessageLocalizer loc, string language)
    {
        var culture = CultureInfo.InvariantCulture;
        var amountStr = $"{m.AmountDue:0.00} {m.Currency?.ToUpperInvariant()}".Trim();

        // Localized headline + intro + footer
        var titleHeadline = loc.Get("email.planPaymentFailed.subject", language);
        var introLine = loc.Get("email.planPaymentFailed.intro", language, new { amount = amountStr, plan = m.PlanName });
        var ctaUpdate = loc.Get("email.planPaymentFailed.cta", language);
        var ifNotYou = loc.Get("shared.if.notYou", language);

        var failedAt = m.FailedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", culture);
        var period = (m.PeriodStartUtc.HasValue && m.PeriodEndUtc.HasValue)
            ? $"{m.PeriodStartUtc.Value:yyyy-MM-dd} → {m.PeriodEndUtc.Value:yyyy-MM-dd}"
            : null;

        var nextAttempt = m.NextPaymentAttemptUtc.HasValue
            ? m.NextPaymentAttemptUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'", culture)
            : null;

        var sb = new StringBuilder();

        sb.Append($@"<!doctype html>
<html>
<head>
  <meta charset='utf-8' />
  <meta name='viewport' content='width=device-width,initial-scale=1' />
  <title>{System.Net.WebUtility.HtmlEncode(titleHeadline)}</title>
</head>
<body style='margin:0;background:#0b1220;font-family:Inter,Segoe UI,Roboto,Arial,sans-serif;'>
  <div style='max-width:680px;margin:0 auto;padding:28px 16px;'>
    <div style='background:linear-gradient(135deg,#111a2e,#0b1220);border:1px solid rgba(255,255,255,.08);border-radius:18px;overflow:hidden;box-shadow:0 18px 50px rgba(0,0,0,.45);'>
      <div style='padding:22px 22px 0 22px;'>
        <div style='display:flex;align-items:center;gap:12px;'>
          <div style='width:44px;height:44px;border-radius:12px;background:rgba(255,77,79,.16);display:flex;align-items:center;justify-content:center;border:1px solid rgba(255,77,79,.25);'>
            <span style='font-size:22px;line-height:1;'>!</span>
          </div>
          <div>
            <div style='color:#e9eefc;font-weight:800;font-size:18px;letter-spacing:.2px;'>{System.Net.WebUtility.HtmlEncode(titleHeadline)}</div>
            <div style='color:rgba(233,238,252,.72);font-size:13px;margin-top:2px;'>Your subscription wasn't renewed. Your plan will be inactive until payment is completed.</div>
          </div>
        </div>
      </div>

      <div style='padding:18px 22px 22px 22px;'>
        <div style='margin-top:14px;background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.08);border-radius:14px;padding:16px;'>
          <div style='color:rgba(233,238,252,.70);font-size:12px;letter-spacing:.8px;text-transform:uppercase;margin-bottom:10px;'>Billing details</div>

          <table role='presentation' style='width:100%;border-collapse:collapse;'>
            <tr>
              <td style='padding:8px 0;color:rgba(233,238,252,.70);font-size:13px;'>Company</td>
              <td style='padding:8px 0;color:#e9eefc;font-size:13px;text-align:right;font-weight:700;'>{Escape(m.CompanyName)}</td>
            </tr>
            <tr>
              <td style='padding:8px 0;color:rgba(233,238,252,.70);font-size:13px;'>Plan</td>
              <td style='padding:8px 0;color:#e9eefc;font-size:13px;text-align:right;font-weight:700;'>{Escape(m.PlanName)}</td>
            </tr>
            <tr>
              <td style='padding:8px 0;color:rgba(233,238,252,.70);font-size:13px;'>Amount due</td>
              <td style='padding:8px 0;color:#ff9aa0;font-size:13px;text-align:right;font-weight:900;'>{Escape(amountStr)}</td>
            </tr>
            <tr>
              <td style='padding:8px 0;color:rgba(233,238,252,.70);font-size:13px;'>Failed at</td>
              <td style='padding:8px 0;color:#e9eefc;font-size:13px;text-align:right;font-weight:700;'>{failedAt}</td>
            </tr>");

        if (!string.IsNullOrWhiteSpace(m.InvoiceNumber))
        {
            sb.Append($@"
            <tr>
              <td style='padding:8px 0;color:rgba(233,238,252,.70);font-size:13px;'>Invoice</td>
              <td style='padding:8px 0;color:#e9eefc;font-size:13px;text-align:right;font-weight:700;'>{Escape(m.InvoiceNumber!)}</td>
            </tr>");
        }

        if (!string.IsNullOrWhiteSpace(period))
        {
            sb.Append($@"
            <tr>
              <td style='padding:8px 0;color:rgba(233,238,252,.70);font-size:13px;'>Billing period</td>
              <td style='padding:8px 0;color:#e9eefc;font-size:13px;text-align:right;font-weight:700;'>{Escape(period)}</td>
            </tr>");
        }

        if (!string.IsNullOrWhiteSpace(nextAttempt))
        {
            sb.Append($@"
            <tr>
              <td style='padding:8px 0;color:rgba(233,238,252,.70);font-size:13px;'>Next attempt</td>
              <td style='padding:8px 0;color:#e9eefc;font-size:13px;text-align:right;font-weight:700;'>{Escape(nextAttempt)}</td>
            </tr>");
        }

        sb.Append(@"
          </table>
        </div>");

        sb.Append(@"
        <div style='margin-top:16px;background:rgba(255,77,79,.10);border:1px solid rgba(255,77,79,.20);border-radius:14px;padding:14px;color:rgba(233,238,252,.82);font-size:12.5px;line-height:1.5;'>
          <strong style='color:#ff9aa0;'>Action required:</strong> complete a new payment to reactivate your plan. Once payment succeeds, MaidsFlow will automatically restore your most recent Stripe subscription and update the official validity date.
        </div>");

        if (!string.IsNullOrWhiteSpace(m.HostedInvoiceUrl) || !string.IsNullOrWhiteSpace(m.InvoicePdfUrl))
        {
            sb.Append(@"
        <div style='margin-top:16px;display:flex;gap:10px;flex-wrap:wrap;'>");

            if (!string.IsNullOrWhiteSpace(m.HostedInvoiceUrl))
            {
                sb.Append($@"
          <a href='{EscapeAttr(m.HostedInvoiceUrl!)}' style='display:inline-block;background:#ff4d4f;color:white;text-decoration:none;font-weight:900;border-radius:12px;padding:12px 14px;font-size:13px;'>Pay invoice</a>");
            }
            if (!string.IsNullOrWhiteSpace(m.InvoicePdfUrl))
            {
                sb.Append($@"
          <a href='{EscapeAttr(m.InvoicePdfUrl!)}' style='display:inline-block;background:rgba(255,255,255,.08);color:#e9eefc;text-decoration:none;font-weight:800;border-radius:12px;padding:12px 14px;font-size:13px;border:1px solid rgba(255,255,255,.10);'>Download PDF</a>");
            }

            sb.Append("</div>");
        }

        var support = string.IsNullOrWhiteSpace(m.SupportUrl) ? null : m.SupportUrl;

        sb.Append(@"
        <div style='margin-top:18px;color:rgba(233,238,252,.65);font-size:12.5px;line-height:1.5;'>
          If you believe this is a mistake (or need help updating your payment method), reply to this email.");

        if (!string.IsNullOrWhiteSpace(support))
        {
            sb.Append($" Or visit our support page: <a href='{EscapeAttr(support!)}' style='color:#9fb3ff;text-decoration:none;font-weight:700;'>Support</a>.");
        }

        sb.Append(@"
        </div>
      </div>

      <div style='padding:14px 22px;border-top:1px solid rgba(255,255,255,.08);color:rgba(233,238,252,.55);font-size:11.5px;line-height:1.4;'>
        This is an automated message. If you did not expect this email, please contact support.
      </div>
    </div>
  </div>
</body>
</html>");

        var subjectSuffix = $"{m.PlanName} • {amountStr}".Trim();

        var plain = new StringBuilder();
        plain.AppendLine(titleHeadline);
        plain.AppendLine(introLine);
        plain.AppendLine("Your subscription wasn't renewed. Your plan will be inactive until payment is completed.");
        plain.AppendLine($"Company: {m.CompanyName}");
        plain.AppendLine($"Plan: {m.PlanName}");
        plain.AppendLine($"Amount due: {amountStr}");
        plain.AppendLine($"Failed at: {failedAt}");
        if (!string.IsNullOrWhiteSpace(m.InvoiceNumber)) plain.AppendLine($"Invoice: {m.InvoiceNumber}");
        if (!string.IsNullOrWhiteSpace(period)) plain.AppendLine($"Billing period: {period}");
        if (!string.IsNullOrWhiteSpace(nextAttempt)) plain.AppendLine($"Next attempt: {nextAttempt}");
        if (!string.IsNullOrWhiteSpace(m.HostedInvoiceUrl)) plain.AppendLine($"Pay invoice: {m.HostedInvoiceUrl}");
        if (!string.IsNullOrWhiteSpace(m.InvoicePdfUrl)) plain.AppendLine($"PDF: {m.InvoicePdfUrl}");
        if (!string.IsNullOrWhiteSpace(support)) plain.AppendLine($"Support: {support}");

        return (subjectSuffix, sb.ToString(), plain.ToString());

        static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        static string EscapeAttr(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
    }
}
