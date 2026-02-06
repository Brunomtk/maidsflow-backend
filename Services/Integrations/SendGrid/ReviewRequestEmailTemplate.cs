using System;
using System.Globalization;
using System.Net;
using System.Text;

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

        public static (string Html, string PlainText) Render(Model m)
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
            sb.AppendLine($"      <div style='font-size:18px;font-weight:700;margin-bottom:8px'>Hi {safeCustomer} 👋</div>");
            sb.AppendLine($"      <div style='font-size:14px;line-height:1.6;color:#cbd5e1'>Your service with <strong style='color:#e5e7eb'>{safeCompany}</strong> was marked as completed. Could you rate it? It takes less than a minute.</div>");
            sb.AppendLine("      <div style='height:16px'></div>");
            sb.AppendLine("      <div style='background:#0b1220;border:1px solid rgba(255,255,255,.08);border-radius:12px;padding:16px'>");
            sb.AppendLine($"        <div style='font-weight:700'>{safeTitle}</div>");
            sb.AppendLine($"        <div style='color:#94a3b8;font-size:13px;margin-top:4px'>{WebUtility.HtmlEncode(when)}</div>");
            if (!string.IsNullOrWhiteSpace(m.AddressLine))
                sb.AppendLine($"        <div style='color:#94a3b8;font-size:13px;margin-top:4px'>{safeAddress}</div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <div style='height:18px'></div>");
            sb.AppendLine($"      <a href='{WebUtility.HtmlEncode(m.ReviewUrl)}' style='display:inline-block;background:#38bdf8;color:#02131b;text-decoration:none;font-weight:700;padding:12px 16px;border-radius:12px'>Leave a review</a>");
            sb.AppendLine("      <div style='height:14px'></div>");
            sb.AppendLine($"      <div style='font-size:12px;color:#94a3b8;line-height:1.5'>If the button doesn't work, copy and paste this link into your browser:<br><span style='word-break:break-all;color:#cbd5e1'>{WebUtility.HtmlEncode(m.ReviewUrl)}</span></div>");
            sb.AppendLine("      <div style='height:18px'></div>");
            sb.AppendLine($"      <div style='font-size:12px;color:#94a3b8'>Need help? <a href='{WebUtility.HtmlEncode(m.SupportUrl)}' style='color:#38bdf8'>Contact support</a></div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div style='text-align:center;color:#64748b;font-size:12px;margin-top:16px'>Sent by MaidsFlow</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</body></html>");

            var plain = $@"Hi {m.CustomerName},\n\nYour service with {m.CompanyName} was marked as completed. Please rate it:\n{m.ReviewUrl}\n\nAppointment: {m.AppointmentTitle}\nWhen: {when}\n{(string.IsNullOrWhiteSpace(m.AddressLine) ? "" : "Where: " + m.AddressLine + "\n")}\nSupport: {m.SupportUrl}\n";

            return (sb.ToString(), plain);
        }
    }
}
