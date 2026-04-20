using System;
using System.Collections.Generic;

namespace Core.DTO.Reports
{
    public class ReportQueryDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? ProfessionalId { get; set; }
        public int? CustomerId { get; set; }
        public int? ServiceTypeId { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class ReportPeriodDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime PreviousStartDate { get; set; }
        public DateTime PreviousEndDate { get; set; }
        public int TotalDays { get; set; }
    }

    public class ReportFilterSnapshotDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? ProfessionalId { get; set; }
        public int? CustomerId { get; set; }
        public int? ServiceTypeId { get; set; }
        public string? Status { get; set; }
        public string DisplayPeriod { get; set; } = string.Empty;
        public List<string> ActiveFilters { get; set; } = new();
    }

    public class ReportExecutiveSummaryDto
    {
        public string Headline { get; set; } = string.Empty;
        public string HealthStatus { get; set; } = "neutral";
        public string Narrative { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> Risks { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
    }

    public class ReportSectionNarrativeDto
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Highlights { get; set; } = new();
        public List<string> Alerts { get; set; } = new();
    }

    public class ReportKpiCardDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string DisplayValue { get; set; } = string.Empty;
        public decimal? ChangePercentage { get; set; }
        public string Trend { get; set; } = "neutral";
        public string? Description { get; set; }
    }

    public class ReportSeriesPointDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class ReportBreakdownItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ReportLeaderboardItemDto
    {
        public int? EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PrimaryValue { get; set; }
        public string PrimaryLabel { get; set; } = string.Empty;
        public decimal? SecondaryValue { get; set; }
        public string? SecondaryLabel { get; set; }
        public string? Badge { get; set; }
    }

    public class ReportBenchmarkDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class ReportTableColumnDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class ReportTableRowDto
    {
        public Dictionary<string, string> Cells { get; set; } = new();
    }

    public class ReportTableDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ReportTableColumnDto> Columns { get; set; } = new();
        public List<ReportTableRowDto> Rows { get; set; } = new();
        public int TotalRows { get; set; }
    }


    public class SendCompanyReportEmailRequestDto
    {
        public int? CompanyId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? RecipientEmail { get; set; }
        public bool UsePreviousMonthByDefault { get; set; } = true;
    }

    public class SendCompanyReportEmailResultDto
    {
        public bool Success { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; }
    }

    public class CompanyReportDto
    {
        public string Scope { get; set; } = "company";
        public DateTime GeneratedAtUtc { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public ReportPeriodDto Period { get; set; } = new();
        public ReportFilterSnapshotDto Filters { get; set; } = new();
        public ReportExecutiveSummaryDto ExecutiveSummary { get; set; } = new();
        public List<ReportKpiCardDto> OverviewCards { get; set; } = new();
        public CompanyReportFinancialDto Financial { get; set; } = new();
        public CompanyReportOperationsDto Operations { get; set; } = new();
        public CompanyReportTeamDto Team { get; set; } = new();
        public CompanyReportCustomersDto Customers { get; set; } = new();
    }

    public class CompanyReportFinancialDto
    {
        public ReportSectionNarrativeDto Narrative { get; set; } = new();
        public List<ReportKpiCardDto> Cards { get; set; } = new();
        public List<ReportBenchmarkDto> Benchmarks { get; set; } = new();
        public List<ReportSeriesPointDto> RevenueTrend { get; set; } = new();
        public List<ReportBreakdownItemDto> PaymentStatusBreakdown { get; set; } = new();
        public List<ReportLeaderboardItemDto> TopCustomersByRevenue { get; set; } = new();
        public ReportTableDto RecentTransactions { get; set; } = new();
    }

    public class CompanyReportOperationsDto
    {
        public ReportSectionNarrativeDto Narrative { get; set; } = new();
        public List<ReportKpiCardDto> Cards { get; set; } = new();
        public List<ReportBenchmarkDto> Benchmarks { get; set; } = new();
        public List<ReportSeriesPointDto> AppointmentsTrend { get; set; } = new();
        public List<ReportBreakdownItemDto> StatusBreakdown { get; set; } = new();
        public List<ReportLeaderboardItemDto> TopServices { get; set; } = new();
        public ReportTableDto RecentAppointments { get; set; } = new();
    }

    public class CompanyReportTeamDto
    {
        public ReportSectionNarrativeDto Narrative { get; set; } = new();
        public List<ReportKpiCardDto> Cards { get; set; } = new();
        public List<ReportBenchmarkDto> Benchmarks { get; set; } = new();
        public List<ReportLeaderboardItemDto> Leaderboard { get; set; } = new();
    }

    public class CompanyReportCustomersDto
    {
        public ReportSectionNarrativeDto Narrative { get; set; } = new();
        public List<ReportKpiCardDto> Cards { get; set; } = new();
        public List<ReportBenchmarkDto> Benchmarks { get; set; } = new();
        public List<ReportLeaderboardItemDto> TopCustomers { get; set; } = new();
        public ReportTableDto CustomerActivityTable { get; set; } = new();
    }

    public class AdminReportDto
    {
        public string Scope { get; set; } = "admin";
        public DateTime GeneratedAtUtc { get; set; }
        public ReportPeriodDto Period { get; set; } = new();
        public ReportFilterSnapshotDto Filters { get; set; } = new();
        public ReportExecutiveSummaryDto ExecutiveSummary { get; set; } = new();
        public List<ReportKpiCardDto> OverviewCards { get; set; } = new();
        public AdminReportBillingDto Billing { get; set; } = new();
        public AdminReportOperationsDto Operations { get; set; } = new();
        public AdminReportCompaniesDto Companies { get; set; } = new();
    }

    public class AdminReportBillingDto
    {
        public ReportSectionNarrativeDto Narrative { get; set; } = new();
        public List<ReportKpiCardDto> Cards { get; set; } = new();
        public List<ReportBenchmarkDto> Benchmarks { get; set; } = new();
        public List<ReportSeriesPointDto> RevenueTrend { get; set; } = new();
        public List<ReportBreakdownItemDto> PaymentStatusBreakdown { get; set; } = new();
        public List<ReportLeaderboardItemDto> CompaniesWithPaymentRisk { get; set; } = new();
    }

    public class AdminReportOperationsDto
    {
        public ReportSectionNarrativeDto Narrative { get; set; } = new();
        public List<ReportKpiCardDto> Cards { get; set; } = new();
        public List<ReportBenchmarkDto> Benchmarks { get; set; } = new();
        public List<ReportSeriesPointDto> AppointmentsTrend { get; set; } = new();
        public List<ReportBreakdownItemDto> StatusBreakdown { get; set; } = new();
    }

    public class AdminReportCompaniesDto
    {
        public ReportSectionNarrativeDto Narrative { get; set; } = new();
        public List<ReportBenchmarkDto> Benchmarks { get; set; } = new();
        public List<ReportLeaderboardItemDto> Ranking { get; set; } = new();
        public ReportTableDto CompaniesTable { get; set; } = new();
    }
}
