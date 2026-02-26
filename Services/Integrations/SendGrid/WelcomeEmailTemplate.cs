using System.Text;

namespace Services.Integrations.SendGrid;

public static class WelcomeEmailTemplate
{
    public sealed record Model(
        string Name,
        string Email,
        string? LoginUrl,
        string? SupportUrl
    );

    public static (string Html, string PlainText) Render(Model m)
    {
        var loginUrl = string.IsNullOrWhiteSpace(m.LoginUrl) ? null : m.LoginUrl;
        var supportUrl = string.IsNullOrWhiteSpace(m.SupportUrl) ? null : m.SupportUrl;

        var html = new StringBuilder();
        html.Append($@"<!doctype html>
<html>
<head>
  <meta charset='utf-8' />
  <meta name='viewport' content='width=device-width,initial-scale=1' />
  <title>Welcome</title>
</head>
<body style='margin:0;background:#0b1220;font-family:Inter,Segoe UI,Roboto,Arial,sans-serif;'>
  <div style='max-width:680px;margin:0 auto;padding:28px 16px;'>
    <div style='background:linear-gradient(135deg,#111a2e,#0b1220);border:1px solid rgba(255,255,255,.08);border-radius:18px;overflow:hidden;box-shadow:0 18px 50px rgba(0,0,0,.45);'>
      <div style='padding:22px;'>
        <div style='color:#e9eefc;font-weight:900;font-size:20px;letter-spacing:.2px;'>Welcome to MaidsFlow</div>
        <div style='color:rgba(233,238,252,.72);font-size:13px;margin-top:8px;line-height:1.5;'>
          Hi <strong style='color:#e9eefc;'>{Escape(m.Name)}</strong>, your account is ready.
          You can log in with <strong style='color:#e9eefc;'>{Escape(m.Email)}</strong>.
        </div>

        <div style='margin-top:16px;background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.08);border-radius:14px;padding:16px;'>
          <div style='color:rgba(233,238,252,.70);font-size:12px;letter-spacing:.8px;text-transform:uppercase;margin-bottom:10px;'>Next steps</div>
          <ol style='margin:0;padding-left:18px;color:rgba(233,238,252,.75);font-size:13px;line-height:1.6;'>
            <li>Log in and complete your company profile</li>
            <li>Set your schedule and services</li>
            <li>Invite your team and start booking</li>
          </ol>
        </div>

        <div style='margin-top:16px;display:flex;gap:10px;flex-wrap:wrap;'>
          {LoginButton(loginUrl)}
          {SupportButton(supportUrl)}
        </div>

        <div style='margin-top:18px;color:rgba(233,238,252,.65);font-size:12.5px;line-height:1.5;'>
          This is an automated message. If you did not create this account, please contact support.
        </div>
      </div>
    </div>
  </div>
</body>
</html>");

        var plain = new StringBuilder();
        plain.AppendLine("Welcome to MaidsFlow");
        plain.AppendLine($"Hi {m.Name}, your account is ready.");
        plain.AppendLine($"Login email: {m.Email}");
        if (!string.IsNullOrWhiteSpace(loginUrl)) plain.AppendLine($"Login: {loginUrl}");
        if (!string.IsNullOrWhiteSpace(supportUrl)) plain.AppendLine($"Support: {supportUrl}");

        return (html.ToString(), plain.ToString());

        static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        static string LoginButton(string? url) => string.IsNullOrWhiteSpace(url)
            ? ""
            : $"<a href='{System.Net.WebUtility.HtmlEncode(url)}' style='display:inline-block;background:#7b61ff;color:white;text-decoration:none;font-weight:900;border-radius:12px;padding:12px 14px;font-size:13px;'>Open MaidsFlow</a>";
        static string SupportButton(string? url) => string.IsNullOrWhiteSpace(url)
            ? ""
            : $"<a href='{System.Net.WebUtility.HtmlEncode(url)}' style='display:inline-block;background:rgba(255,255,255,.08);color:#e9eefc;text-decoration:none;font-weight:900;border-radius:12px;padding:12px 14px;font-size:13px;border:1px solid rgba(255,255,255,.10);'>Support</a>";
    }
}
