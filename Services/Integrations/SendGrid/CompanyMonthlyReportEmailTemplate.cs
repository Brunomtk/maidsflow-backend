using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Core.DTO.Reports;

namespace Services.Integrations.SendGrid;

public static class CompanyMonthlyReportEmailTemplate
{
    public sealed record Model(
        string CompanyName,
        string RecipientEmail,
        string PeriodLabel,
        DateTime GeneratedAtUtc,
        string ExecutiveNarrative,
        string HealthStatus,
        IReadOnlyList<ReportKpiCardDto> OverviewCards,
        IReadOnlyList<ReportKpiCardDto> FinancialCards,
        IReadOnlyList<ReportKpiCardDto> OperationsCards,
        IReadOnlyList<ReportKpiCardDto> TeamCards,
        IReadOnlyList<ReportKpiCardDto> CustomerCards,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> Risks,
        IReadOnlyList<string> RecommendedActions,
        string? SupportUrl
    );

    public sealed record RenderedEmail(string Subject, string Html, string PlainText);

    public static RenderedEmail Render(Model model, string subject)
    {
        var html = BuildHtml(model, subject);
        var plain = BuildPlainText(model, subject);
        return new RenderedEmail(subject, html, plain);
    }

    private static string BuildHtml(Model model, string subject)
    {
        var generatedAt = model.GeneratedAtUtc.ToString("MMM dd, yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var summaryCards = model.OverviewCards?.Take(4).ToArray() ?? Array.Empty<ReportKpiCardDto>();

        var sb = new StringBuilder();
        sb.Append("<!doctype html>");
        sb.Append("<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>");
        sb.Append(E(subject));
        sb.Append("</title></head>");
        sb.Append("<body style=\"margin:0;padding:0;background:#eef4f8;font-family:Inter,Segoe UI,Arial,Helvetica,sans-serif;color:#0f172a;\">");
        sb.Append("<div style=\"padding:28px 12px;background:#eef4f8;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:760px;margin:0 auto;border-collapse:separate;\">");
        sb.Append("<tr><td>");

        sb.Append("<div style=\"padding:10px 6px 18px;color:#5b6b7a;font-size:12px;letter-spacing:.08em;text-transform:uppercase;text-align:center;\">MaidsFlow · Monthly business report</div>");

        sb.Append("<div style=\"background:linear-gradient(135deg,#0f172a 0%,#12314d 52%,#1e88c7 100%);border-radius:30px 30px 0 0;padding:34px 34px 30px;box-shadow:0 20px 50px rgba(15,23,42,.12);\">");
        sb.Append("<div style=\"display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;\">");
        sb.Append("<div>");
        sb.Append("<div style=\"display:inline-block;padding:8px 14px;border-radius:999px;background:rgba(255,255,255,.12);color:#e2f3ff;font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;\">Automated delivery</div>");
        sb.Append($"<h1 style=\"margin:18px 0 10px;color:#ffffff;font-size:30px;line-height:1.18;font-weight:800;\">{E(subject)}</h1>");
        sb.Append($"<p style=\"margin:0;max-width:560px;color:rgba(255,255,255,.86);font-size:15px;line-height:1.75;\">A polished monthly snapshot for <strong>{E(model.CompanyName)}</strong>, covering the period <strong>{E(model.PeriodLabel)}</strong>.</p>");
        sb.Append("</div>");
        sb.Append($"<div style=\"min-width:170px;background:rgba(255,255,255,.1);border:1px solid rgba(255,255,255,.14);border-radius:22px;padding:18px 18px 16px;\"><div style=\"font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:#c8e7fb;margin-bottom:8px;\">Generated</div><div style=\"font-size:15px;font-weight:700;color:#ffffff;line-height:1.5;\">{E(generatedAt)}</div></div>");
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("<div style=\"background:#ffffff;border:1px solid #d8e6ef;border-top:none;border-radius:0 0 30px 30px;overflow:hidden;box-shadow:0 20px 50px rgba(15,23,42,.08);\">");
        sb.Append("<div style=\"padding:32px 30px 10px;\">");

        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-bottom:24px;border-collapse:separate;border-spacing:0;\"><tr>");
        sb.Append($"<td valign=\"top\" style=\"padding:0 8px 12px 0;\"><div style=\"background:#f8fbfd;border:1px solid #dce9f1;border-radius:22px;padding:20px 22px;\"><div style=\"font-size:11px;color:#6b7b8b;text-transform:uppercase;letter-spacing:.08em;margin-bottom:8px;\">Company</div><div style=\"font-size:22px;font-weight:800;color:#102033;line-height:1.2;\">{E(model.CompanyName)}</div><div style=\"margin-top:8px;font-size:13px;color:#5e7285;line-height:1.7;\">Monthly executive email crafted to match the MaidsFlow reporting experience.</div></div></td>");
        sb.Append($"<td valign=\"top\" style=\"padding:0 0 12px 8px;\"><div style=\"background:{BadgeSurface(model.HealthStatus)};border:1px solid {BadgeBorder(model.HealthStatus)};border-radius:22px;padding:20px 22px;\"><div style=\"font-size:11px;color:{BadgeFg(model.HealthStatus)};text-transform:uppercase;letter-spacing:.08em;margin-bottom:8px;\">Health status</div><div style=\"font-size:22px;font-weight:800;color:{BadgeFg(model.HealthStatus)};line-height:1.2;\">{E(string.IsNullOrWhiteSpace(model.HealthStatus) ? "Neutral" : model.HealthStatus)}</div><div style=\"margin-top:8px;font-size:13px;color:#5e7285;line-height:1.7;\">Use this summary as a quick reference before opening the full dashboard.</div></div></td>");
        sb.Append("</tr></table>");

        sb.Append("<div style=\"background:linear-gradient(180deg,#f8fbfd 0%,#ffffff 100%);border:1px solid #dde8f1;border-radius:24px;padding:24px 24px 22px;margin-bottom:24px;\">");
        sb.Append("<div style=\"font-size:12px;font-weight:800;color:#1e88c7;letter-spacing:.08em;text-transform:uppercase;margin-bottom:10px;\">Executive summary</div>");
        sb.Append($"<div style=\"font-size:16px;line-height:1.9;color:#334155;\">{E(model.ExecutiveNarrative)}</div>");
        sb.Append("</div>");

        if (summaryCards.Length > 0)
        {
            sb.Append("<div style=\"margin-bottom:22px;\"><div style=\"font-size:18px;font-weight:800;color:#102033;margin-bottom:14px;\">At a glance</div>");
            AppendCardGrid(sb, summaryCards, 4, true);
            sb.Append("</div>");
        }

        AppendSection(sb, "Financial performance", "Revenue, payments, and billing momentum for the selected period.", model.FinancialCards, "#eff8ff", "#0b78b8");
        AppendSection(sb, "Operations snapshot", "Appointments, delivery consistency, and operational flow.", model.OperationsCards, "#f3f0ff", "#7c3aed");
        AppendSection(sb, "Team highlights", "Capacity, output, and professional performance indicators.", model.TeamCards, "#ecfeff", "#0f766e");
        AppendSection(sb, "Customer view", "Retention, quality signals, and customer activity signals.", model.CustomerCards, "#fff7ed", "#c2410c");

        AppendInsightPanel(sb, "What is going well", model.Strengths, "#ecfdf5", "#047857", "#a7f3d0");
        AppendInsightPanel(sb, "Points of attention", model.Risks, "#fff7ed", "#c2410c", "#fed7aa");
        AppendInsightPanel(sb, "Recommended next actions", model.RecommendedActions, "#eff6ff", "#1d4ed8", "#bfdbfe");

        if (!string.IsNullOrWhiteSpace(model.SupportUrl))
        {
            sb.Append("<div style=\"padding:10px 0 24px;\">");
            sb.Append($"<a href=\"{E(model.SupportUrl!)}\" style=\"display:inline-block;background:linear-gradient(135deg,#0f172a 0%,#1e88c7 100%);color:#ffffff;text-decoration:none;padding:15px 24px;border-radius:16px;font-size:14px;font-weight:800;letter-spacing:.01em;\">Open full report in MaidsFlow</a>");
            sb.Append("</div>");
        }

        sb.Append("</div>");
        sb.Append("<div style=\"padding:22px 30px;background:#f8fbfd;border-top:1px solid #e2ecf2;\">");
        sb.Append($"<div style=\"font-size:12px;color:#5d7084;line-height:1.8;\">This message was sent automatically by MaidsFlow to <strong>{E(model.RecipientEmail)}</strong>. You can trigger this report manually at any time or keep the scheduled delivery on the first day of each month.</div>");
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("</td></tr></table></div></body></html>");
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, string subtitle, IReadOnlyList<ReportKpiCardDto> cards, string accentSurface, string accentColor)
    {
        if (cards == null || cards.Count == 0) return;

        sb.Append("<div style=\"margin:0 0 22px;\">\n");
        sb.Append($"<div style=\"display:flex;align-items:flex-end;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:14px;\"><div><div style=\"font-size:18px;font-weight:800;color:#102033;\">{E(title)}</div><div style=\"margin-top:6px;font-size:13px;color:#607487;line-height:1.6;\">{E(subtitle)}</div></div><div style=\"padding:8px 12px;border-radius:999px;background:{accentSurface};color:{accentColor};font-size:11px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;\">Monthly block</div></div>");
        AppendCardGrid(sb, cards.Take(4).ToArray(), 2, false, accentSurface, accentColor);
        sb.Append("</div>");
    }

    private static void AppendCardGrid(StringBuilder sb, IReadOnlyList<ReportKpiCardDto> cards, int columns, bool compact, string accentSurface = "#f8fbfd", string accentColor = "#0f172a")
    {
        if (cards == null || cards.Count == 0) return;

        var items = cards.ToArray();
        var width = columns <= 2 ? "50%" : "25%";
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;border-spacing:10px;margin:0 -10px;\"><tr>");
        for (var i = 0; i < items.Length; i++)
        {
            var card = items[i];
            sb.Append($"<td valign=\"top\" style=\"width:{width};padding:0;\">");
            sb.Append("<div style=\"height:100%;background:#ffffff;border:1px solid #dce8f0;border-radius:20px;padding:18px 18px 16px;box-shadow:0 8px 20px rgba(15,23,42,.04);\">");
            sb.Append($"<div style=\"display:inline-block;padding:6px 10px;border-radius:999px;background:{accentSurface};color:{accentColor};font-size:10px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;margin-bottom:12px;\">{E(card.Label)}</div>");
            sb.Append($"<div style=\"font-size:{(compact ? "24px" : "28px")};font-weight:800;color:#102033;line-height:1.15;letter-spacing:-.02em;\">{E(card.DisplayValue)}</div>");
            if (card.ChangePercentage.HasValue)
                sb.Append($"<div style=\"margin-top:10px;font-size:12px;font-weight:800;color:{ChangeColor(card.ChangePercentage.Value)};\">{E(FormatSignedPct(card.ChangePercentage.Value))} vs previous period</div>");
            if (!string.IsNullOrWhiteSpace(card.Description))
                sb.Append($"<div style=\"margin-top:10px;font-size:12px;line-height:1.7;color:#64748b;\">{E(card.Description!)}</div>");
            sb.Append("</div></td>");
        }
        sb.Append("</tr></table>");
    }

    private static void AppendInsightPanel(StringBuilder sb, string title, IReadOnlyList<string> items, string background, string titleColor, string borderColor)
    {
        if (items == null || items.Count == 0) return;
        sb.Append($"<div style=\"margin-top:18px;background:{background};border:1px solid {borderColor};border-radius:24px;padding:22px 22px 18px;\">");
        sb.Append($"<div style=\"font-size:17px;font-weight:800;color:{titleColor};margin-bottom:12px;\">{E(title)}</div>");
        sb.Append("<ul style=\"margin:0;padding-left:18px;color:#334155;\">");
        foreach (var item in items.Take(4))
            sb.Append($"<li style=\"margin:0 0 10px;line-height:1.8;\">{E(item)}</li>");
        sb.Append("</ul></div>");
    }

    private static string BuildPlainText(Model model, string subject)
    {
        var sb = new StringBuilder();
        sb.AppendLine(subject);
        sb.AppendLine($"Company: {model.CompanyName}");
        sb.AppendLine($"Period: {model.PeriodLabel}");
        sb.AppendLine($"Generated: {model.GeneratedAtUtc.ToString("MMM dd, yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)}");
        sb.AppendLine();
        sb.AppendLine(model.ExecutiveNarrative);
        sb.AppendLine();
        AppendPlainSection(sb, "Overview", model.OverviewCards);
        AppendPlainSection(sb, "Financial", model.FinancialCards);
        AppendPlainSection(sb, "Operations", model.OperationsCards);
        AppendPlainSection(sb, "Team", model.TeamCards);
        AppendPlainSection(sb, "Customers", model.CustomerCards);
        AppendPlainBullets(sb, "What is going well", model.Strengths);
        AppendPlainBullets(sb, "Points of attention", model.Risks);
        AppendPlainBullets(sb, "Recommended next actions", model.RecommendedActions);
        return sb.ToString();
    }

    private static void AppendPlainSection(StringBuilder sb, string title, IReadOnlyList<ReportKpiCardDto> cards)
    {
        if (cards == null || cards.Count == 0) return;
        sb.AppendLine(title.ToUpperInvariant());
        foreach (var card in cards.Take(4))
            sb.AppendLine($"- {card.Label}: {card.DisplayValue}{(card.ChangePercentage.HasValue ? $" ({FormatSignedPct(card.ChangePercentage.Value)} vs previous period)" : string.Empty)}");
        sb.AppendLine();
    }

    private static void AppendPlainBullets(StringBuilder sb, string title, IReadOnlyList<string> items)
    {
        if (items == null || items.Count == 0) return;
        sb.AppendLine(title.ToUpperInvariant());
        foreach (var item in items.Take(4))
            sb.AppendLine($"- {item}");
        sb.AppendLine();
    }

    private static string E(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string BadgeSurface(string? status) => (status ?? string.Empty).ToLowerInvariant() switch { "good" => "#ecfdf5", "risk" => "#fff7ed", "attention" => "#fff7ed", "critical" => "#fef2f2", _ => "#eff6ff" };
    private static string BadgeBorder(string? status) => (status ?? string.Empty).ToLowerInvariant() switch { "good" => "#a7f3d0", "risk" => "#fed7aa", "attention" => "#fed7aa", "critical" => "#fecaca", _ => "#bfdbfe" };
    private static string BadgeFg(string? status) => (status ?? string.Empty).ToLowerInvariant() switch { "good" => "#047857", "risk" => "#c2410c", "attention" => "#c2410c", "critical" => "#b91c1c", _ => "#1d4ed8" };
    private static string ChangeColor(decimal value) => value > 0 ? "#059669" : value < 0 ? "#dc2626" : "#64748b";
    private static string FormatSignedPct(decimal value) => $"{(value >= 0 ? "+" : string.Empty)}{value:0.0}%";
}
