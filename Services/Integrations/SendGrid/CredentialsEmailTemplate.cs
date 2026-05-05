using System;
using System.Net;
using Services.Localization;

namespace Services.Integrations.SendGrid;

public static class CredentialsEmailTemplate
{
    public sealed record Payload(
        string CompanyName,
        string UserName,
        string Email,
        string Password,
        string Role,
        string LoginUrl
    );

    public sealed record Rendered(
        string Subject,
        string PlainText,
        string Html
    );

    /// <summary>
    /// Renders the credentials email in the recipient's language.
    /// All UI labels come from <see cref="IMessageLocalizer"/> (keys under <c>email.credentials.*</c>
    /// and <c>shared.*</c>); HTML structure / layout / colors are language-agnostic.
    /// </summary>
    public static Rendered Render(Payload p, IMessageLocalizer loc, string language)
    {
        // Localized strings
        var subject = loc.Get("email.credentials.subject", language, new { company = p.CompanyName });
        var greeting = loc.Get("shared.greeting.hello", language, new { name = p.UserName });
        var intro = loc.Get("email.credentials.intro", language, new { company = p.CompanyName });
        var labelEmail = loc.Get("email.credentials.fields.email", language);
        var labelPassword = loc.Get("email.credentials.fields.password", language);
        var labelRole = loc.Get("email.credentials.fields.role", language);
        var labelLogin = loc.Get("email.credentials.fields.login", language);
        var ctaLabel = loc.Get("email.credentials.cta", language);
        var ifNotYou = loc.Get("shared.if.notYou", language);

        // HTML-encoded values (already safely escaped for HTML; raw values used in plain text)
        var company = WebUtility.HtmlEncode(p.CompanyName);
        var userName = WebUtility.HtmlEncode(p.UserName);
        var email = WebUtility.HtmlEncode(p.Email);
        var role = WebUtility.HtmlEncode(p.Role);
        var loginUrl = WebUtility.HtmlEncode(p.LoginUrl);
        var password = WebUtility.HtmlEncode(p.Password);

        // Plain text (uses the raw localized strings; line breaks are real \n)
        var plain = $"{greeting},\n\n" +
                    $"{intro}\n\n" +
                    $"{labelEmail}: {p.Email}\n" +
                    $"{labelPassword}: {p.Password}\n" +
                    $"{labelRole}: {p.Role}\n\n" +
                    $"{labelLogin}: {p.LoginUrl}\n\n" +
                    $"{ifNotYou}";

        // HTML — same layout as before; only inner copy is now localized.
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
    .header {{ padding:28px 28px 18px 28px; background: linear-gradient(135deg, rgba(33,150,243,0.25), rgba(0,188,212,0.10)); }}
    .brand {{ font-size:14px; letter-spacing:0.12em; text-transform:uppercase; color:#bfe9ff; opacity:0.9; }}
    .title {{ margin:10px 0 0 0; font-size:22px; color:#ffffff; }}
    .sub {{ margin:8px 0 0 0; font-size:14px; color:rgba(255,255,255,0.75); line-height:1.5; }}
    .content {{ padding:22px 28px 28px 28px; }}
    .row {{ margin:14px 0; }}
    .label {{ font-size:12px; color:rgba(255,255,255,0.65); margin-bottom:6px; }}
    .value {{ background: rgba(255,255,255,0.06); border:1px solid rgba(255,255,255,0.08); padding:12px 14px; border-radius:12px; color:#ffffff; font-size:14px; word-break: break-word; }}
    .badge {{ display:inline-block; padding:6px 10px; border-radius:999px; font-size:12px; background: rgba(0, 188, 212, 0.18); color:#b7fbff; border:1px solid rgba(0,188,212,0.25); }}
    .btn {{ display:inline-block; margin-top:18px; padding:12px 16px; border-radius:12px; background:#00bcd4; color:#001018; text-decoration:none; font-weight:700; }}
    .footer {{ padding:18px 28px; background: rgba(255,255,255,0.03); border-top:1px solid rgba(255,255,255,0.06); font-size:12px; color:rgba(255,255,255,0.6); line-height:1.5; }}
    .muted a {{ color:#bfe9ff; }}
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
          <div class=""label"">{WebUtility.HtmlEncode(labelRole)}</div>
          <div class=""badge"">{role}</div>
        </div>

        <div class=""row"">
          <div class=""label"">{WebUtility.HtmlEncode(labelEmail)}</div>
          <div class=""value"">{email}</div>
        </div>

        <div class=""row"">
          <div class=""label"">{WebUtility.HtmlEncode(labelPassword)}</div>
          <div class=""value""><b>{password}</b></div>
        </div>

        <a class=""btn"" href=""{loginUrl}"">{WebUtility.HtmlEncode(ctaLabel)}</a>
      </div>
      <div class=""footer muted"">
        {WebUtility.HtmlEncode(ifNotYou)}
        <br/>{WebUtility.HtmlEncode(labelLogin)}: <a href=""{loginUrl}"">{loginUrl}</a>
      </div>
    </div>
  </div>
</body>
</html>";

        return new Rendered(subject, plain, html);
    }
}
