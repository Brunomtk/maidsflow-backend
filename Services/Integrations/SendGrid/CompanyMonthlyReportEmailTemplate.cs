using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Core.DTO.Reports;

using Services.Localization;

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

    public static RenderedEmail Render(Model model, string subject, IMessageLocalizer loc, string language)
    {
        var html = BuildHtml(model, subject, loc, language);
        var plain = BuildPlainText(model, subject, loc, language);
        return new RenderedEmail(subject, html, plain);
    }

    private static string BuildHtml(Model model, string subject, IMessageLocalizer loc, string language)
    {
        var generatedAt = model.GeneratedAtUtc.ToString("MMM dd, yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var summaryCards = model.OverviewCards?.Take(4).ToArray() ?? Array.Empty<ReportKpiCardDto>();

        // Localized labels (rest of the layout uses model.* dynamic values)


        var lblMonthlyReport = loc.Get("pdf.monthlyReport.title", language);


        var lblPeriod = loc.Get("pdf.monthlyReport.period", language);


        var lblCompanyTag = loc.Get("pdf.monthlyReport.section.summary", language);


        var lblFooter = loc.Get("pdf.monthlyReport.footer", language, new { date = DateTime.UtcNow.ToString("yyyy-MM-dd") });

        // Section labels + subtitles + insight panel labels + footer text
        var lblHealthStatus = loc.Get("report.email.healthStatus", language);
        var lblHealthNeutral = loc.Get("report.email.healthNeutral", language);
        var lblHealthHint = loc.Get("report.email.healthHint", language);
        var lblExecutiveSummary = loc.Get("report.email.executiveSummary", language);
        var lblAtAGlance = loc.Get("report.email.atAGlance", language);
        var lblFinancialTitle = loc.Get("report.email.financial.title", language);
        var lblFinancialSubtitle = loc.Get("report.email.financial.subtitle", language);
        var lblOperationsTitle = loc.Get("report.email.operations.title", language);
        var lblOperationsSubtitle = loc.Get("report.email.operations.subtitle", language);
        var lblTeamTitle = loc.Get("report.email.team.title", language);
        var lblTeamSubtitle = loc.Get("report.email.team.subtitle", language);
        var lblCustomersTitle = loc.Get("report.email.customers.title", language);
        var lblCustomersSubtitle = loc.Get("report.email.customers.subtitle", language);
        var lblWhatGoingWell = loc.Get("report.email.whatGoingWell", language);
        var lblPointsAttention = loc.Get("report.email.pointsAttention", language);
        var lblRecommendedActions = loc.Get("report.email.recommendedActions", language);
        var lblOpenReportCta = loc.Get("report.email.openReportCta", language);
        var lblFooterMessage = loc.Get("report.email.footerMessage", language, new { email = model.RecipientEmail });
        var lblPlainCompany = loc.Get("report.email.plainCompany", language);
        var lblPlainPeriod = loc.Get("report.email.plainPeriod", language);
        var lblPlainGenerated = loc.Get("report.email.plainGenerated", language);
        var lblPlainOverview = loc.Get("report.email.plainOverview", language);
        var lblPlainFinancial = loc.Get("report.email.plainFinancial", language);
        var lblPlainOperations = loc.Get("report.email.plainOperations", language);
        var lblPlainTeam = loc.Get("report.email.plainTeam", language);
        var lblPlainCustomers = loc.Get("report.email.plainCustomers", language);




        var sb = new StringBuilder();
        sb.Append("<!doctype html>");
        sb.Append("<html lang=\"en\" style=\"color-scheme:only light;supported-color-schemes:light;\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"color-scheme\" content=\"light only\"><meta name=\"supported-color-schemes\" content=\"light\"><title>");
        sb.Append(E(subject));
        sb.Append("</title><style>:root{color-scheme:only light;supported-color-schemes:light;}body,table,td,div,p,a{color-scheme:only light;}body{margin:0!important;padding:0!important;background:#eef4f8!important;color:#0f172a!important;}@media (prefers-color-scheme: dark){body,.mf-page{background:#eef4f8!important;color:#0f172a!important;}.mf-shell,.mf-card{background:#ffffff!important;color:#0f172a!important;}.mf-soft{background:#f8fbfd!important;}.mf-muted{color:#5e7285!important;}.mf-title{color:#102033!important;}}</style></head>");
        sb.Append("<body bgcolor=\"#eef4f8\" style=\"margin:0;padding:0;background:#eef4f8!important;font-family:Inter,Segoe UI,Arial,Helvetica,sans-serif;color:#0f172a!important;color-scheme:only light;supported-color-schemes:light;\">");
        sb.Append("<div class=\"mf-page\" style=\"padding:28px 12px;background:#eef4f8!important;color:#0f172a!important;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:760px;margin:0 auto;border-collapse:separate;\">");
        sb.Append("<tr><td>");

        sb.Append($"<div class=\"mf-muted\" style=\"padding:10px 6px 18px;color:#5b6b7a!important;font-size:12px;letter-spacing:.08em;text-transform:uppercase;text-align:center;\">{E(lblMonthlyReport)}</div>");

        sb.Append("<div class=\"mf-shell\" style=\"background:#ffffff!important;border:1px solid #d8e6ef;border-bottom:none;border-radius:30px 30px 0 0;padding:34px 34px 30px;box-shadow:0 20px 50px rgba(15,23,42,.08);\">");
        sb.Append("<div style=\"display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;\">");
        sb.Append("<div>");
        sb.Append("<div style=\"display:inline-block;padding:8px 14px;border-radius:999px;background:#eff8ff!important;color:#0b78b8!important;font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;\">Automated delivery</div>");
        sb.Append($"<h1 class=\"mf-title\" style=\"margin:18px 0 10px;color:#102033!important;font-size:30px;line-height:1.18;font-weight:800;\">{E(subject)}</h1>");
        sb.Append($"<p class=\"mf-muted\" style=\"margin:0;max-width:560px;color:#5e7285!important;font-size:15px;line-height:1.75;\">A polished monthly snapshot for <strong style=\"color:#102033!important;\">{E(model.CompanyName)}</strong>, covering the period <strong style=\"color:#102033!important;\">{E(model.PeriodLabel)}</strong>.</p>");
        sb.Append("</div>");
        sb.Append($"<div class=\"mf-soft\" style=\"min-width:170px;background:#f8fbfd!important;border:1px solid #dce9f1;border-radius:22px;padding:18px 18px 16px;\"><div class=\"mf-muted\" style=\"font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:#6b7b8b!important;margin-bottom:8px;\">Generated</div><div class=\"mf-title\" style=\"font-size:15px;font-weight:700;color:#102033!important;line-height:1.5;\">{E(generatedAt)}</div></div>");
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("<div class=\"mf-shell\" style=\"background:#ffffff!important;border:1px solid #d8e6ef;border-top:none;border-radius:0 0 30px 30px;overflow:hidden;box-shadow:0 20px 50px rgba(15,23,42,.08);\">");
        sb.Append("<div style=\"padding:32px 30px 10px;\">");

        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-bottom:24px;border-collapse:separate;border-spacing:0;\"><tr>");
        sb.Append($"<td valign=\"top\" style=\"padding:0 8px 12px 0;\"><div class=\"mf-soft\" style=\"background:#f8fbfd!important;border:1px solid #dce9f1;border-radius:22px;padding:20px 22px;\"><div style=\"font-size:11px;color:#6b7b8b!important;text-transform:uppercase;letter-spacing:.08em;margin-bottom:8px;\">{E(lblCompanyTag)}</div><div style=\"font-size:22px;font-weight:800;color:#102033!important;line-height:1.2;\">{E(model.CompanyName)}</div><div style=\"margin-top:8px;font-size:13px;color:#5e7285!important;line-height:1.7;\">{E(lblMonthlyReport)}</div></div></td>");
        sb.Append($"<td valign=\"top\" style=\"padding:0 0 12px 8px;\"><div style=\"background:{BadgeSurface(model.HealthStatus)};border:1px solid {BadgeBorder(model.HealthStatus)};border-radius:22px;padding:20px 22px;\"><div style=\"font-size:11px;color:{BadgeFg(model.HealthStatus)};text-transform:uppercase;letter-spacing:.08em;margin-bottom:8px;\">{E(lblHealthStatus)}</div><div style=\"font-size:22px;font-weight:800;color:{BadgeFg(model.HealthStatus)};line-height:1.2;\">{E(string.IsNullOrWhiteSpace(model.HealthStatus) ? lblHealthNeutral : model.HealthStatus)}</div><div style=\"margin-top:8px;font-size:13px;color:#5e7285!important;line-height:1.7;\">{E(lblHealthHint)}</div></div></td>");
        sb.Append("</tr></table>");

        sb.Append("<div class=\"mf-soft\" style=\"background:#f8fbfd!important;border:1px solid #dde8f1;border-radius:24px;padding:24px 24px 22px;margin-bottom:24px;\">");
        sb.Append("<div style=\"font-size:12px;font-weight:800;color:#1e88c7;letter-spacing:.08em;text-transform:uppercase;margin-bottom:10px;\">{E(lblExecutiveSummary)}</div>");
        sb.Append($"<div style=\"font-size:16px;line-height:1.9;color:#334155!important;\">{E(model.ExecutiveNarrative)}</div>");
        sb.Append("</div>");

        if (summaryCards.Length > 0)
        {
            sb.Append("<div style=\"margin-bottom:22px;\"><div style=\"font-size:18px;font-weight:800;color:#102033!important;margin-bottom:14px;\">{E(lblAtAGlance)}</div>");
            AppendCardGrid(sb, summaryCards, 4, true);
            sb.Append("</div>");
        }

        AppendSection(sb, lblFinancialTitle, lblFinancialSubtitle, model.FinancialCards, "#eff8ff", "#0b78b8", lblMonthlyReport);
        AppendSection(sb, lblOperationsTitle, lblOperationsSubtitle, model.OperationsCards, "#f3f0ff", "#7c3aed", lblMonthlyReport);
        AppendSection(sb, lblTeamTitle, lblTeamSubtitle, model.TeamCards, "#ecfeff", "#0f766e", lblMonthlyReport);
        AppendSection(sb, lblCustomersTitle, lblCustomersSubtitle, model.CustomerCards, "#fff7ed", "#c2410c", lblMonthlyReport);

        AppendInsightPanel(sb, lblWhatGoingWell, model.Strengths, "#ecfdf5", "#047857", "#a7f3d0");
        AppendInsightPanel(sb, lblPointsAttention, model.Risks, "#fff7ed", "#c2410c", "#fed7aa");
        AppendInsightPanel(sb, lblRecommendedActions, model.RecommendedActions, "#eff6ff", "#1d4ed8", "#bfdbfe");

        if (!string.IsNullOrWhiteSpace(model.SupportUrl))
        {
            sb.Append("<div style=\"padding:10px 0 24px;\">");
            sb.Append($"<a href=\"{E(model.SupportUrl!)}\" style=\"display:inline-block;background:linear-gradient(135deg,#0f172a 0%,#1e88c7 100%);color:#ffffff;text-decoration:none;padding:15px 24px;border-radius:16px;font-size:14px;font-weight:800;letter-spacing:.01em;\">{E(lblOpenReportCta)}</a>");
            sb.Append("</div>");
        }

        sb.Append("</div>");
        sb.Append("<div class=\"mf-soft\" style=\"padding:22px 30px;background:#f8fbfd!important;border-top:1px solid #e2ecf2;\">");
        sb.Append($"<div style=\"font-size:12px;color:#5d7084!important;line-height:1.8;\">{E(lblFooterMessage)}</div>");
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("</td></tr></table></div></body></html>");
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, string subtitle, IReadOnlyList<ReportKpiCardDto> cards, string accentSurface, string accentColor, string monthlyReportLabel)
    {
        if (cards == null || cards.Count == 0) return;

        sb.Append("<div style=\"margin:0 0 22px;\">\n");
        sb.Append($"<div style=\"display:flex;align-items:flex-end;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:14px;\"><div><div style=\"font-size:18px;font-weight:800;color:#102033!important;\">{E(title)}</div><div style=\"margin-top:6px;font-size:13px;color:#607487!important;line-height:1.6;\">{E(subtitle)}</div></div><div style=\"padding:8px 12px;border-radius:999px;background:{accentSurface};color:{accentColor};font-size:11px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;\">{E(monthlyReportLabel)}</div></div>");
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
            sb.Append("<div class=\"mf-card\" style=\"height:100%;background:#ffffff!important;border:1px solid #dce8f0;border-radius:20px;padding:18px 18px 16px;box-shadow:0 8px 20px rgba(15,23,42,.04);\">");
            sb.Append($"<div style=\"display:inline-block;padding:6px 10px;border-radius:999px;background:{accentSurface};color:{accentColor};font-size:10px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;margin-bottom:12px;\">{E(card.Label)}</div>");
            sb.Append($"<div style=\"font-size:{(compact ? "24px" : "28px")};font-weight:800;color:#102033!important;line-height:1.15;letter-spacing:-.02em;\">{E(card.DisplayValue)}</div>");
            if (card.ChangePercentage.HasValue)
                sb.Append($"<div style=\"margin-top:10px;font-size:12px;font-weight:800;color:{ChangeColor(card.ChangePercentage.Value)};\">{E(FormatSignedPct(card.ChangePercentage.Value))} vs previous period</div>");
            if (!string.IsNullOrWhiteSpace(card.Description))
                sb.Append($"<div style=\"margin-top:10px;font-size:12px;line-height:1.7;color:#64748b!important;\">{E(card.Description!)}</div>");
            sb.Append("</div></td>");
        }
        sb.Append("</tr></table>");
    }

    private static void AppendInsightPanel(StringBuilder sb, string title, IReadOnlyList<string> items, string background, string titleColor, string borderColor)
    {
        if (items == null || items.Count == 0) return;
        sb.Append($"<div style=\"margin-top:18px;background:{background};border:1px solid {borderColor};border-radius:24px;padding:22px 22px 18px;\">");
        sb.Append($"<div style=\"font-size:17px;font-weight:800;color:{titleColor};margin-bottom:12px;\">{E(title)}</div>");
        sb.Append("<ul style=\"margin:0;padding-left:18px;color:#334155!important;\">");
        foreach (var item in items.Take(4))
            sb.Append($"<li style=\"margin:0 0 10px;line-height:1.8;\">{E(item)}</li>");
        sb.Append("</ul></div>");
    }

    private static string BuildPlainText(Model model, string subject, IMessageLocalizer loc, string language)
    {
        // Localized labels (mirrored from BuildHtml — kept here so this method is self-contained)
        var lblPlainCompany = loc.Get("report.email.plainCompany", language);
        var lblPlainPeriod = loc.Get("report.email.plainPeriod", language);
        var lblPlainGenerated = loc.Get("report.email.plainGenerated", language);
        var lblPlainOverview = loc.Get("report.email.plainOverview", language);
        var lblPlainFinancial = loc.Get("report.email.plainFinancial", language);
        var lblPlainOperations = loc.Get("report.email.plainOperations", language);
        var lblPlainTeam = loc.Get("report.email.plainTeam", language);
        var lblPlainCustomers = loc.Get("report.email.plainCustomers", language);
        var lblWhatGoingWell = loc.Get("report.email.whatGoingWell", language);
        var lblPointsAttention = loc.Get("report.email.pointsAttention", language);
        var lblRecommendedActions = loc.Get("report.email.recommendedActions", language);

        var sb = new StringBuilder();
        sb.AppendLine(subject);
        sb.AppendLine($"{lblPlainCompany}: {model.CompanyName}");
        sb.AppendLine($"{lblPlainPeriod}: {model.PeriodLabel}");
        sb.AppendLine($"{lblPlainGenerated}: {model.GeneratedAtUtc.ToString("MMM dd, yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)}");
        sb.AppendLine();
        sb.AppendLine(model.ExecutiveNarrative);
        sb.AppendLine();
        AppendPlainSection(sb, lblPlainOverview, model.OverviewCards);
        AppendPlainSection(sb, lblPlainFinancial, model.FinancialCards);
        AppendPlainSection(sb, lblPlainOperations, model.OperationsCards);
        AppendPlainSection(sb, lblPlainTeam, model.TeamCards);
        AppendPlainSection(sb, lblPlainCustomers, model.CustomerCards);
        AppendPlainBullets(sb, lblWhatGoingWell, model.Strengths);
        AppendPlainBullets(sb, lblPointsAttention, model.Risks);
        AppendPlainBullets(sb, lblRecommendedActions, model.RecommendedActions);
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
