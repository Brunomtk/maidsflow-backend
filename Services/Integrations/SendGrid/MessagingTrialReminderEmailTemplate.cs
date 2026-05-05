using System.Net;

namespace Services.Integrations.SendGrid;

/// <summary>
/// Renders the "your SMS trial is ending" email — sent at D-2 and D-1 by the
/// MessagingTrialReminderHostedService. Tiles match the dark MaidsFlow look.
/// </summary>
public static class MessagingTrialReminderEmailTemplate
{
    public sealed record Payload(
        string CompanyName,
        string RecipientName,
        int DaysLeft,                     // 2 or 1
        DateTime TrialEndsAtUtc,
        string PortalUrl                  // e.g. https://app.maidsflow.com/company/sms-setup
    );

    public sealed record Rendered(
        string Subject,
        string PlainText,
        string Html
    );

    public static Rendered Render(Payload p, string language)
    {
        var lang = (language ?? "en").ToLowerInvariant();

        // ---- i18n strings (en / pt-br / es / fr) ----
        var (subject, hello, headline, bodyIntro, whyMatters, whatToDo, ctaLabel,
             daysWord, helpHint, footer, deadlineLabel)
             = lang switch
        {
            "pt-br" or "pt" => (
                p.DaysLeft == 1
                    ? $"Falta 1 dia para o fim do seu período de teste de SMS"
                    : $"Faltam {p.DaysLeft} dias para o fim do seu período de teste de SMS",
                $"Olá {p.RecipientName}",
                p.DaysLeft == 1
                    ? "1 dia restante no seu trial de SMS"
                    : $"{p.DaysLeft} dias restantes no seu trial de SMS",
                $"O período de teste gratuito de SMS da {p.CompanyName} no MaidsFlow termina em breve. Após o vencimento, sem a aprovação Twilio A2P 10DLC, suas mensagens automáticas para clientes deixarão de ser enviadas.",
                "Por que isso importa?",
                "Para continuar enviando lembretes, atualizações de horário e notificações de chegada via SMS, finalize seu cadastro no centro de Compliance: preencha os dados do seu Business e envie os documentos. A equipe MaidsFlow cuida do resto com a Twilio.",
                "Concluir cadastro de SMS agora",
                p.DaysLeft == 1 ? "dia" : "dias",
                "Se já enviou tudo e está aguardando análise, pode ignorar este email — vamos te avisar assim que aprovado.",
                "Você está recebendo este email porque sua empresa está cadastrada no MaidsFlow.",
                "Trial encerra em"
            ),
            "es" or "es-es" => (
                p.DaysLeft == 1
                    ? "Queda 1 día para el fin de tu prueba de SMS"
                    : $"Quedan {p.DaysLeft} días para el fin de tu prueba de SMS",
                $"Hola {p.RecipientName}",
                p.DaysLeft == 1
                    ? "1 día restante en tu prueba de SMS"
                    : $"{p.DaysLeft} días restantes en tu prueba de SMS",
                $"El período de prueba gratuito de SMS de {p.CompanyName} en MaidsFlow está por terminar. Sin la aprobación Twilio A2P 10DLC, tus mensajes automáticos a clientes dejarán de enviarse.",
                "¿Por qué importa?",
                "Para seguir enviando recordatorios, actualizaciones de horario y notificaciones de llegada por SMS, completa tu registro en el centro de Compliance: llena los datos de tu Business y carga los documentos. El equipo de MaidsFlow se encarga del resto con Twilio.",
                "Completar registro de SMS",
                p.DaysLeft == 1 ? "día" : "días",
                "Si ya enviaste todo y está en revisión, puedes ignorar este correo — te avisaremos al aprobarse.",
                "Recibes este correo porque tu empresa está registrada en MaidsFlow.",
                "Prueba termina"
            ),
            "fr" or "fr-fr" => (
                p.DaysLeft == 1
                    ? "Plus qu'1 jour avant la fin de votre essai SMS"
                    : $"Plus que {p.DaysLeft} jours avant la fin de votre essai SMS",
                $"Bonjour {p.RecipientName}",
                p.DaysLeft == 1
                    ? "1 jour restant sur votre essai SMS"
                    : $"{p.DaysLeft} jours restants sur votre essai SMS",
                $"La période d'essai gratuite SMS de {p.CompanyName} sur MaidsFlow se termine bientôt. Sans l'approbation Twilio A2P 10DLC, vos messages automatiques aux clients ne seront plus envoyés.",
                "Pourquoi est-ce important ?",
                "Pour continuer à envoyer rappels, mises à jour d'horaire et notifications d'arrivée par SMS, terminez votre inscription dans le centre de Compliance : remplissez les informations de Business et téléchargez les documents. L'équipe MaidsFlow s'occupe du reste avec Twilio.",
                "Terminer l'inscription SMS",
                p.DaysLeft == 1 ? "jour" : "jours",
                "Si vous avez déjà tout envoyé et que c'est en cours de revue, ignorez cet email — nous vous préviendrons à l'approbation.",
                "Vous recevez cet email car votre entreprise est inscrite sur MaidsFlow.",
                "L'essai se termine"
            ),
            _ => (
                p.DaysLeft == 1
                    ? "1 day left on your SMS trial"
                    : $"{p.DaysLeft} days left on your SMS trial",
                $"Hi {p.RecipientName}",
                p.DaysLeft == 1
                    ? "1 day left on your SMS trial"
                    : $"{p.DaysLeft} days left on your SMS trial",
                $"The free SMS trial for {p.CompanyName} on MaidsFlow is about to end. Without Twilio A2P 10DLC approval, your automated reminders, schedule updates and arrival notifications will stop going out.",
                "Why this matters",
                "To keep sending SMS to your customers, finish onboarding in the Compliance center: fill in your Business profile and upload the supporting documents. The MaidsFlow team handles the rest with Twilio.",
                "Finish SMS setup now",
                p.DaysLeft == 1 ? "day" : "days",
                "If you've already submitted everything and it's under review, you can ignore this — we'll email you the moment it's approved.",
                "You're receiving this because your company is registered on MaidsFlow.",
                "Trial ends"
            ),
        };

        var company   = WebUtility.HtmlEncode(p.CompanyName);
        var name      = WebUtility.HtmlEncode(p.RecipientName ?? "");
        var portalUrl = WebUtility.HtmlEncode(p.PortalUrl);
        var endsLocal = p.TrialEndsAtUtc.ToString("MMM d, yyyy 'at' HH:mm 'UTC'");
        var daysBig   = p.DaysLeft.ToString();

        var pillColor = p.DaysLeft <= 1 ? "#ff6b6b" : "#f7b955";
        var pillBg    = p.DaysLeft <= 1 ? "rgba(255,107,107,.12)" : "rgba(247,185,85,.14)";
        var pillBord  = p.DaysLeft <= 1 ? "rgba(255,107,107,.35)" : "rgba(247,185,85,.45)";

        var plain =
            $"{hello},\n\n" +
            $"{headline}\n\n" +
            $"{bodyIntro}\n\n" +
            $"{whyMatters}\n{whatToDo}\n\n" +
            $"{ctaLabel}: {p.PortalUrl}\n\n" +
            $"{deadlineLabel}: {endsLocal}\n\n" +
            $"{helpHint}\n\n" +
            $"— MaidsFlow\n";

        var html = $@"<!doctype html>
<html lang=""{WebUtility.HtmlEncode(lang)}"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{WebUtility.HtmlEncode(subject)}</title>
</head>
<body style=""margin:0;padding:0;background:#0b1220;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#cfe3ff;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:#0b1220;padding:32px 16px;"">
    <tr>
      <td align=""center"">

        <!-- Outer card -->
        <table role=""presentation"" width=""640"" cellspacing=""0"" cellpadding=""0"" style=""max-width:640px;background:linear-gradient(180deg,#0f1b2d 0%,#0c1626 100%);border:1px solid rgba(255,255,255,.08);border-radius:18px;overflow:hidden;"">

          <!-- Brand bar -->
          <tr>
            <td style=""padding:22px 28px;background:linear-gradient(90deg,rgba(24,190,200,.18),rgba(143,93,255,.10) 60%,transparent);border-bottom:1px solid rgba(255,255,255,.05);"">
              <div style=""display:flex;align-items:center;justify-content:space-between;gap:12px;"">
                <div style=""display:flex;align-items:center;gap:10px;"">
                  <div style=""width:36px;height:36px;border-radius:10px;background:linear-gradient(135deg,#18bec8,#1689e3);display:inline-flex;align-items:center;justify-content:center;font-size:18px;color:#031018;font-weight:800;"">📲</div>
                  <div style=""font-size:13px;letter-spacing:.4px;color:#9fb3c8;text-transform:uppercase;font-weight:700;"">Maids Flow · SMS Compliance</div>
                </div>
                <div style=""font-size:11px;color:#9fb3c8;text-align:right;"">{WebUtility.HtmlEncode(deadlineLabel)}<br/><span style=""color:#ffffff;font-weight:600;"">{WebUtility.HtmlEncode(endsLocal)}</span></div>
              </div>
            </td>
          </tr>

          <!-- Headline -->
          <tr>
            <td style=""padding:30px 28px 6px 28px;"">
              <div style=""display:inline-block;padding:6px 12px;border-radius:999px;background:{pillBg};border:1px solid {pillBord};color:{pillColor};font-size:12px;font-weight:700;letter-spacing:.2px;"">
                ⏳ {daysBig} {WebUtility.HtmlEncode(daysWord)}
              </div>
              <h1 style=""margin:14px 0 4px 0;font-size:26px;line-height:1.25;color:#ffffff;font-weight:800;"">{WebUtility.HtmlEncode(headline)}</h1>
              <p style=""margin:8px 0 0 0;color:#9fb3c8;font-size:14px;"">{WebUtility.HtmlEncode(hello)},</p>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style=""padding:18px 28px 6px 28px;"">
              <p style=""margin:0;font-size:15px;line-height:1.6;color:#cfe3ff;"">
                {WebUtility.HtmlEncode(bodyIntro)}
              </p>
            </td>
          </tr>

          <!-- Why card -->
          <tr>
            <td style=""padding:14px 28px 4px 28px;"">
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:rgba(255,255,255,.03);border:1px solid rgba(255,255,255,.06);border-radius:14px;"">
                <tr>
                  <td style=""padding:16px 18px;"">
                    <div style=""font-size:12px;color:#9fb3c8;text-transform:uppercase;letter-spacing:.4px;font-weight:700;"">{WebUtility.HtmlEncode(whyMatters)}</div>
                    <div style=""margin-top:8px;font-size:14px;color:#e8f1ff;line-height:1.6;"">{WebUtility.HtmlEncode(whatToDo)}</div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- CTA -->
          <tr>
            <td style=""padding:22px 28px 8px 28px;"">
              <a href=""{portalUrl}"" style=""display:inline-block;background:linear-gradient(90deg,#18bec8,#1689e3);color:#03101a;text-decoration:none;font-weight:800;font-size:15px;padding:14px 22px;border-radius:14px;box-shadow:0 8px 24px rgba(24,190,200,.25);"">
                {WebUtility.HtmlEncode(ctaLabel)} →
              </a>
              <div style=""margin-top:12px;font-size:12px;color:#6f86a0;"">
                {WebUtility.HtmlEncode(p.PortalUrl)}
              </div>
            </td>
          </tr>

          <!-- Help hint -->
          <tr>
            <td style=""padding:14px 28px 28px 28px;"">
              <div style=""font-size:12px;color:#7d92ad;line-height:1.6;border-top:1px solid rgba(255,255,255,.06);padding-top:14px;"">
                {WebUtility.HtmlEncode(helpHint)}
              </div>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""padding:16px 28px;background:rgba(0,0,0,.22);"">
              <div style=""font-size:11px;color:#6f86a0;line-height:1.6;"">
                © {company} · Powered by <strong style=""color:#cfe3ff;"">Maids Flow</strong><br/>
                {WebUtility.HtmlEncode(footer)}
              </div>
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
