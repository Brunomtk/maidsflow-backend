using System;
using System.Net;

namespace Services.Integrations.SendGrid;

public static class WelcomeEmailTemplate
{
    public sealed record Payload(
        string CompanyName,
        string UserName,
        string Email,
        string Role,
        string LoginUrl,
        DateTime CreatedAtUtc
    );

    public sealed record Rendered(
        string Subject,
        string PlainText,
        string Html
    );

    public static Rendered Render(Payload p, string subject)
    {
        var company = WebUtility.HtmlEncode(p.CompanyName);
        var userName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(p.UserName) ? "there" : p.UserName);
        var email = WebUtility.HtmlEncode(p.Email);
        var role = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(p.Role) ? "user" : p.Role);
        var loginUrl = WebUtility.HtmlEncode(p.LoginUrl);
        var createdAt = p.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");

        var plain =
            $"Hello {p.UserName},\n\n" +
            $"Your MaidsFlow account has been created successfully.\n\n" +
            $"Company: {p.CompanyName}\n" +
            $"Email: {p.Email}\n" +
            $"Role: {p.Role}\n" +
            $"Created: {createdAt}\n\n" +
            $"Sign in here: {p.LoginUrl}\n\n" +
            "Tip: If you don't know your password yet, use the 'Forgot password' option on the login page.\n\n" +
            "If you did not expect this email, please contact support.";

        var html = $@"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{WebUtility.HtmlEncode(subject)}</title>
  <style>
    body {{ margin:0; padding:0; background:#0b1220; font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Arial,sans-serif; }}
    .wrap {{ width:100%; padding:32px 12px; }}
    .card {{ max-width:680px; margin:0 auto; background:#0f1b2e; border:1px solid rgba(255,255,255,0.08); border-radius:20px; overflow:hidden; box-shadow:0 10px 30px rgba(0,0,0,0.35); }}
    .header {{ padding:30px 28px 20px 28px; background: radial-gradient(900px 350px at 10% 10%, rgba(0,188,212,0.20), transparent 55%), linear-gradient(135deg, rgba(33,150,243,0.22), rgba(0,188,212,0.10)); }}
    .brand {{ display:flex; gap:10px; align-items:center; font-size:13px; letter-spacing:0.12em; text-transform:uppercase; color:#bfe9ff; opacity:0.95; }}
    .dot {{ width:10px; height:10px; border-radius:999px; background:#00bcd4; box-shadow:0 0 0 4px rgba(0,188,212,0.15); }}
    .title {{ margin:12px 0 0 0; font-size:24px; color:#ffffff; }}
    .sub {{ margin:10px 0 0 0; font-size:14px; color:rgba(255,255,255,0.78); line-height:1.55; }}
    .content {{ padding:22px 28px 28px 28px; }}
    .grid {{ display:grid; grid-template-columns: 1fr 1fr; gap:14px; margin-top:14px; }}
    .item {{ background: rgba(255,255,255,0.06); border:1px solid rgba(255,255,255,0.08); border-radius:14px; padding:14px; }}
    .label {{ font-size:12px; color:rgba(255,255,255,0.62); margin-bottom:6px; }}
    .value {{ font-size:14px; color:#ffffff; word-break:break-word; }}
    .badge {{ display:inline-block; padding:6px 10px; border-radius:999px; font-size:12px; background: rgba(0,188,212,0.18); color:#b7fbff; border:1px solid rgba(0,188,212,0.25); }}
    .btn {{ display:inline-block; margin-top:18px; padding:12px 16px; border-radius:12px; background:#00bcd4; color:#001018; text-decoration:none; font-weight:800; }}
    .note {{ margin-top:16px; padding:14px 14px; border-radius:14px; background: rgba(33,150,243,0.10); border:1px solid rgba(33,150,243,0.18); color:rgba(255,255,255,0.82); font-size:13px; line-height:1.55; }}
    .footer {{ padding:18px 28px; background: rgba(255,255,255,0.03); border-top:1px solid rgba(255,255,255,0.06); font-size:12px; color:rgba(255,255,255,0.6); line-height:1.6; }}
    .footer a {{ color:#bfe9ff; }}
    @media (max-width: 520px) {{ .grid {{ grid-template-columns: 1fr; }} }}
  </style>
</head>
<body>
  <div class=""wrap"">
    <div class=""card"">
      <div class=""header"">
        <div class=""brand""><span class=""dot""></span> {company}</div>
        <h1 class=""title"">Your account is ready ✨</h1>
        <p class=""sub"">Hi <b>{userName}</b> — your MaidsFlow account was created successfully. Use the details below to confirm everything is correct, then sign in.</p>
      </div>
      <div class=""content"">
        <div class=""grid"">
          <div class=""item"">
            <div class=""label"">Email</div>
            <div class=""value"">{email}</div>
          </div>
          <div class=""item"">
            <div class=""label"">Role</div>
            <div class=""value""><span class=""badge"">{role}</span></div>
          </div>
          <div class=""item"">
            <div class=""label"">Created</div>
            <div class=""value"">{WebUtility.HtmlEncode(createdAt)}</div>
          </div>
          <div class=""item"">
            <div class=""label"">Company</div>
            <div class=""value"">{company}</div>
          </div>
        </div>

        <a class=""btn"" href=""{loginUrl}"">Sign in to MaidsFlow</a>

        <div class=""note"">
          <b>First time signing in?</b> If you don't know your password yet, use the <b>Forgot password</b> option on the login page.
          <br/>If you didn't expect this email, please contact support.
        </div>
      </div>
      <div class=""footer"">
        Login URL: <a href=""{loginUrl}"">{loginUrl}</a>
      </div>
    </div>
  </div>
</body>
</html>";

        return new Rendered(subject, plain, html);
    }
}
