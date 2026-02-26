using System.Net;

namespace Services.Integrations.SendGrid;

/// <summary>
/// Renders an email for password reset (forgot password).
/// </summary>
public static class PasswordResetEmailTemplate
{
    public sealed record Payload(
        string CompanyName,
        string UserName,
        string Email,
        string ResetUrl
    );

    public sealed record Rendered(
        string Subject,
        string PlainText,
        string Html
    );

    public static Rendered Render(Payload p, string subject)
    {
        var company = WebUtility.HtmlEncode(p.CompanyName);
        var userName = WebUtility.HtmlEncode(p.UserName);
        var email = WebUtility.HtmlEncode(p.Email);
        var resetUrl = WebUtility.HtmlEncode(p.ResetUrl);

        var plain =
            $"Hi {p.UserName},\n\n" +
            $"We received a request to reset your MaidsFlow password.\n\n" +
            $"Reset your password using this link:\n{p.ResetUrl}\n\n" +
            $"If you didn't request this, you can ignore this email.\n\n" +
            $"Account: {p.Email}\n";

        var html = $@"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{WebUtility.HtmlEncode(subject)}</title>
</head>
<body style=""margin:0;padding:0;background:#0b1220;font-family:Arial,Helvetica,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:#0b1220;padding:32px 16px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""640"" cellspacing=""0"" cellpadding=""0"" style=""max-width:640px;background:#0f1b2d;border:1px solid rgba(255,255,255,.08);border-radius:16px;overflow:hidden;"">
          <tr>
            <td style=""padding:28px 28px 10px 28px;"">
              <div style=""font-size:14px;color:#9fb3c8;letter-spacing:.3px;"">{company}</div>
              <div style=""font-size:26px;color:#ffffff;font-weight:700;margin-top:10px;"">Reset your password</div>
              <div style=""font-size:15px;color:#cfe3ff;line-height:1.5;margin-top:12px;"">
                Hi <b>{userName}</b>, we received a request to reset your password.
              </div>
            </td>
          </tr>

          <tr>
            <td style=""padding:10px 28px 28px 28px;"">
              <a href=""{resetUrl}"" style=""display:inline-block;background:#18bec8;color:#061018;text-decoration:none;font-weight:700;padding:12px 18px;border-radius:12px;"">
                Reset Password
              </a>

              <div style=""font-size:13px;color:#9fb3c8;line-height:1.6;margin-top:16px;"">
                This link will expire soon for security reasons. If you didn't request this, you can safely ignore this email.
              </div>

              <div style=""margin-top:18px;padding:14px 14px;border-radius:12px;background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.06);"">
                <div style=""font-size:12px;color:#9fb3c8;"">Account</div>
                <div style=""font-size:14px;color:#ffffff;"">{email}</div>
              </div>

              <div style=""font-size:12px;color:#6f86a0;line-height:1.6;margin-top:18px;"">
                If the button doesn't work, copy and paste this URL into your browser:<br/>
                <span style=""color:#cfe3ff;"">{resetUrl}</span>
              </div>
            </td>
          </tr>

          <tr>
            <td style=""padding:18px 28px;background:rgba(0,0,0,.18);color:#6f86a0;font-size:12px;"">
              © {company} • MaidsFlow
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

        return new Rendered(subject, plain, html);
    }
}
