using System;
using System.Net;
using Services.Localization;

namespace Services.Integrations.SendGrid;

/// <summary>
/// Renders an email notifying the user that their password was changed, in the recipient's language.
/// </summary>
public static class PasswordChangedEmailTemplate
{
    public sealed record Payload(
        string CompanyName,
        string UserName,
        string Email,
        string LoginUrl,
        DateTime ChangedAtUtc
    );

    public sealed record Rendered(
        string Subject,
        string PlainText,
        string Html
    );

    public static Rendered Render(Payload p, IMessageLocalizer loc, string language)
    {
        var subject = loc.Get("email.passwordChanged.subject", language);
        var greeting = loc.Get("shared.greeting.hello", language, new { name = p.UserName });
        var intro = loc.Get("email.passwordChanged.intro", language);
        var tip = loc.Get("email.passwordChanged.tip", language);
        var labelEmail = loc.Get("email.credentials.fields.email", language);
        var ctaLogin = loc.Get("email.credentials.cta", language);
        var labelLogin = loc.Get("email.credentials.fields.login", language);

        var company = WebUtility.HtmlEncode(p.CompanyName);
        var userName = WebUtility.HtmlEncode(p.UserName);
        var email = WebUtility.HtmlEncode(p.Email);
        var loginUrl = WebUtility.HtmlEncode(p.LoginUrl);
        var changedAt = p.ChangedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");

        var plain = $"{greeting},\n\n" +
                    $"{intro} ({changedAt})\n\n" +
                    $"{tip}\n\n" +
                    $"{labelEmail}: {p.Email}\n" +
                    $"{labelLogin}: {p.LoginUrl}\n";

        var html = $@"<!doctype html>
<html lang=""{WebUtility.HtmlEncode(language)}"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{WebUtility.HtmlEncode(subject)}</title>
  <style>
    body {{ margin:0; padding:0; background:#0b1220; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif; }}
    .wrap {{ width:100%; padding:32px 12px; }}
    .card {{ max-width:640px; margin:0 auto; background:#0f1b2e; border:1px solid rgba(255,255,255,0.08); border-radius:18px; overflow:hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.35); }}
    .header {{ padding:28px 28px 18px 28px; background: linear-gradient(135deg, rgba(255,152,0,0.20), rgba(244,67,54,0.10)); }}
    .brand {{ font-size:14px; letter-spacing:0.12em; text-transform:uppercase; color:#ffe0b2; opacity:0.95; }}
    .title {{ margin:10px 0 0 0; font-size:22px; color:#ffffff; }}
    .sub {{ margin:8px 0 0 0; font-size:14px; color:rgba(255,255,255,0.78); line-height:1.5; }}
    .content {{ padding:22px 28px 28px 28px; }}
    .row {{ margin:14px 0; }}
    .label {{ font-size:12px; color:rgba(255,255,255,0.65); margin-bottom:6px; }}
    .value {{ background: rgba(255,255,255,0.06); border:1px solid rgba(255,255,255,0.08); padding:12px 14px; border-radius:12px; color:#ffffff; font-size:14px; word-break: break-word; }}
    .alert {{ margin-top:14px; background: rgba(244,67,54,0.10); border:1px solid rgba(244,67,54,0.25); border-radius:14px; padding:12px 14px; color:#ffd7d7; font-size:14px; line-height:1.45; }}
    .btn {{ display:inline-block; margin-top:18px; padding:12px 16px; border-radius:12px; background:#ff9800; color:#1a0f00; text-decoration:none; font-weight:800; }}
    .footer {{ padding:18px 28px; background: rgba(255,255,255,0.03); border-top:1px solid rgba(255,255,255,0.06); font-size:12px; color:rgba(255,255,255,0.6); line-height:1.5; }}
    .muted a {{ color:#ffe0b2; }}
  </style>
</head>
<body>
  <div class=""wrap"">
    <div class=""card"">
      <div class=""header"">
        <div class=""brand"">{company}</div>
        <h1 class=""title"">{WebUtility.HtmlEncode(subject)}</h1>
        <p class=""sub"">{WebUtility.HtmlEncode(greeting)} — {WebUtility.HtmlEncode(intro)}</p>
      </div>

      <div class=""content"">
        <div class=""row"">
          <div class=""label"">{WebUtility.HtmlEncode(labelEmail)}</div>
          <div class=""value"">{email}</div>
        </div>

        <div class=""row"">
          <div class=""label"">UTC</div>
          <div class=""value"">{WebUtility.HtmlEncode(changedAt)}</div>
        </div>

        <div class=""alert"">
          {WebUtility.HtmlEncode(tip)}
        </div>

        <a class=""btn"" href=""{loginUrl}"">{WebUtility.HtmlEncode(ctaLogin)}</a>
      </div>

      <div class=""footer muted"">
        {WebUtility.HtmlEncode(labelLogin)}: <a href=""{loginUrl}"">{loginUrl}</a>
      </div>
    </div>
  </div>
</body>
</html>";

        return new Rendered(subject, plain, html);
    }
}
