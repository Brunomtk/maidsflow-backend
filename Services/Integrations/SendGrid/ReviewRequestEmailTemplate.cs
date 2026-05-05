using System;
using System.Globalization;
using System.Net;
using System.Text;
using Services.Localization;

namespace Services.Integrations.SendGrid
{
    public static class ReviewRequestEmailTemplate
    {
        public record Model(
            string CustomerName,
            string CompanyName,
            string AppointmentTitle,
            DateTime AppointmentStartLocal,
            string? AddressLine,
            string ReviewUrl,
            string SupportUrl
        );

        public static (string Html, string PlainText) Render(Model m, IMessageLocalizer loc, string language)
        {
            var safeCustomer = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(m.CustomerName) ? "there" : m.CustomerName);
            var safeCompany = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(m.CompanyName) ? "MaidsFlow" : m.CompanyName);
            var safeTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(m.AppointmentTitle) ? "Your appointment" : m.AppointmentTitle);
            var safeAddress = WebUtility.HtmlEncode(m.AddressLine ?? string.Empty);
            var when = m.AppointmentStartLocal.ToString("MMM dd, yyyy 'at' HH:mm", CultureInfo.InvariantCulture);

            var sb = new StringBuilder();

            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>");
            sb.AppendLine("<title>Review</title></head>");
            sb.AppendLine("<body style='margin:0;background:#0b1220;font-family:system-ui,-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#e5e7eb'>");
            sb.AppendLine("  <div style='max-width:640px;margin:0 auto;padding:24px'>");
            sb.AppendLine("    <div style='background:#0f172a;border:1px solid rgba(255,255,255,.08);border-radius:16px;padding:24px'>");
            var introText = loc.Get("email.reviewRequest.intro", language, new { customer = m.CustomerName, company = m.CompanyName });
            var bodyText = loc.Get("email.reviewRequest.body", language);
            var ctaText = loc.Get("email.reviewRequest.cta", language);
            sb.AppendLine($"      <div style='font-size:18px;font-weight:700;margin-bottom:8px'>{WebUtility.HtmlEncode(introText)} 👋</div>");
            sb.AppendLine($"      <div style='font-size:14px;line-height:1.6;color:#cbd5e1'>{WebUtility.HtmlEncode(bodyText)}</div>");
            sb.AppendLine("      <div style='height:16px'></div>");
            sb.AppendLine("      <div style='background:#0b1220;border:1px solid rgba(255,255,255,.08);border-radius:12px;padding:16px'>");
            sb.AppendLine($"        <div style='font-weight:700'>{safeTitle}</div>");
            sb.AppendLine($"        <div style='color:#94a3b8;font-size:13px;margin-top:4px'>{WebUtility.HtmlEncode(when)}</div>");
            if (!string.IsNullOrWhiteSpace(m.AddressLine))
                sb.AppendLine($"        <div style='color:#94a3b8;font-size:13px;margin-top:4px'>{safeAddress}</div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <div style='height:18px'></div>");
            sb.AppendLine($"      <a href='{WebUtility.HtmlEncode(m.ReviewUrl)}' style='display:inline-block;background:#38bdf8;color:#02131b;text-decoration:none;font-weight:700;padding:12px 16px;border-radius:12px'>{WebUtility.HtmlEncode(ctaText)}</a>");
            sb.AppendLine("      <div style='height:14px'></div>");
            sb.AppendLine($"      <div style='font-size:12px;color:#94a3b8;line-height:1.5'>If the button doesn't work, copy and paste this link into your browser:<br><span style='word-break:break-all;color:#cbd5e1'>{WebUtility.HtmlEncode(m.ReviewUrl)}</span></div>");
            sb.AppendLine("      <div style='height:18px'></div>");
            sb.AppendLine($"      <div style='font-size:12px;color:#94a3b8'>Need help? <a href='{WebUtility.HtmlEncode(m.SupportUrl)}' style='color:#38bdf8'>Contact support</a></div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div style='text-align:center;color:#64748b;font-size:12px;margin-top:16px'>Sent by MaidsFlow</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</body></html>");

            var plainSb = new StringBuilder();
            plainSb.AppendLine(introText);
            plainSb.AppendLine();
            plainSb.AppendLine(bodyText);
            plainSb.AppendLine();
            plainSb.AppendLine($"{ctaText}: {m.ReviewUrl}");
            plainSb.AppendLine();
            plainSb.AppendLine(m.AppointmentTitle ?? string.Empty);
            plainSb.AppendLine(when);
            if (!string.IsNullOrWhiteSpace(m.AddressLine))
                plainSb.AppendLine(m.AddressLine);
            plainSb.AppendLine($"Support: {m.SupportUrl}");
            var plain = plainSb.ToString();

            return (sb.ToString(), plain);
        }
    }
}
