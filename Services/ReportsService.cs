using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DTO.Reports;
using Core.Enums;
using Core.Enums.Appointment;
using Core.Enums.Payment;
using Core.Enums.Plan;
using Core.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Services.Security;

namespace Services
{
    public interface IReportsService
    {
        Task<CompanyReportDto> GetCompanyReportAsync(ReportQueryDto query);
        Task<AdminReportDto> GetAdminReportAsync(ReportQueryDto query);
        Task<byte[]> ExportCompanyReportCsvAsync(ReportQueryDto query);
        Task<byte[]> ExportAdminReportCsvAsync(ReportQueryDto query);
    }

    public class ReportsService : IReportsService
    {
        private readonly DbContextClass _db;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public ReportsService(DbContextClass db, ICurrentUser currentUser, IScopeGuard scope)
        {
            _db = db;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<CompanyReportDto> GetCompanyReportAsync(ReportQueryDto query)
        {
            var companyId = await ResolveCompanyIdAsync();
            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == companyId)
                ?? throw new InvalidOperationException("Company not found.");

            var period = BuildPeriod(query);
            var previousQuery = BuildPreviousQuery(query, period);

            var appointments = await GetAppointmentsAsync(companyId, query);
            var previousAppointments = await GetAppointmentsAsync(companyId, previousQuery);
            var payments = await GetPaymentsAsync(companyId, query);
            var previousPayments = await GetPaymentsAsync(companyId, previousQuery);
            var customers = await _db.Customers.AsNoTracking().Where(x => x.CompanyId == companyId).ToListAsync();
            var professionals = await _db.Professionals.AsNoTracking().Where(x => x.CompanyId == companyId).ToListAsync();
            var reviews = await _db.Reviews.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.Date >= period.StartDate && x.Date <= period.EndDate)
                .ToListAsync();
            var serviceTypes = await _db.ServiceTypes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);

            var paidPayments = payments.Where(x => x.Status == PaymentStatus.Paid).ToList();
            var previousPaidPayments = previousPayments.Where(x => x.Status == PaymentStatus.Paid).ToList();

            var appointmentTotal = appointments.Count;
            var previousAppointmentTotal = previousAppointments.Count;
            var completedCount = appointments.Count(x => x.Status == AppointmentStatus.Completed);
            var previousCompletedCount = previousAppointments.Count(x => x.Status == AppointmentStatus.Completed);
            var cancelledCount = appointments.Count(x => x.Status == AppointmentStatus.Cancelled);
            var previousCancelledCount = previousAppointments.Count(x => x.Status == AppointmentStatus.Cancelled);
            var scheduledCount = appointments.Count(x => x.Status == AppointmentStatus.Scheduled);
            var inProgressCount = appointments.Count(x => x.Status == AppointmentStatus.InProgress);
            var completedRevenue = paidPayments.Sum(x => x.Amount);
            var previousCompletedRevenue = previousPaidPayments.Sum(x => x.Amount);
            var receivableAmount = payments.Where(x => x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Overdue).Sum(x => x.Amount);
            var overdueAmount = payments.Where(x => x.Status == PaymentStatus.Overdue).Sum(x => x.Amount);
            var totalBilledAmount = payments.Sum(x => x.Amount);
            var collectionRate = totalBilledAmount > 0 ? completedRevenue / totalBilledAmount * 100m : 0m;
            var averageTicket = appointmentTotal > 0 ? completedRevenue / appointmentTotal : 0m;
            var completionRate = appointmentTotal > 0 ? completedCount / (decimal)appointmentTotal * 100m : 0m;
            var cancellationRate = appointmentTotal > 0 ? cancelledCount / (decimal)appointmentTotal * 100m : 0m;
            var previousCompletionRate = previousAppointmentTotal > 0 ? previousCompletedCount / (decimal)previousAppointmentTotal * 100m : 0m;
            var activeCustomerIds = appointments.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).Distinct().ToHashSet();
            var newCustomers = customers.Count(x => x.CreatedDate >= period.StartDate && x.CreatedDate <= period.EndDate);
            var recurringCustomerCount = appointments.Where(x => x.CustomerId.HasValue)
                .GroupBy(x => x.CustomerId!.Value)
                .Count(g => g.Count() > 1);
            var averageRating = reviews.Any() ? (decimal)reviews.Average(x => x.Rating) : 0m;
            var recurringAppointments = appointments.Count(x => x.IsRecurring);
            var recurringShare = appointmentTotal > 0 ? recurringAppointments / (decimal)appointmentTotal * 100m : 0m;
            var dailyAverageAppointments = period.TotalDays > 0 ? appointmentTotal / (decimal)period.TotalDays : 0m;
            var revenuePerActiveCustomer = activeCustomerIds.Count > 0 ? completedRevenue / activeCustomerIds.Count : 0m;

            var teamRows = BuildProfessionalRows(appointments, paidPayments, professionals, reviews);
            var customerRows = BuildCustomerRevenueRows(appointments, paidPayments, customers);
            var serviceRows = appointments
                .GroupBy(x => x.ServiceTypeId)
                .Select(g => new ReportLeaderboardItemDto
                {
                    EntityId = g.Key,
                    Name = g.Key.HasValue && serviceTypes.TryGetValue(g.Key.Value, out var serviceName) ? serviceName : "No service",
                    PrimaryValue = g.Count(),
                    PrimaryLabel = "appointments",
                    SecondaryValue = g.Count(x => x.Status == AppointmentStatus.Completed),
                    SecondaryLabel = "completed",
                    Badge = g.Count(x => x.IsRecurring) > 0 ? $"{g.Count(x => x.IsRecurring)} recurring" : null,
                })
                .OrderByDescending(x => x.PrimaryValue)
                .ThenByDescending(x => x.SecondaryValue)
                .ToList();

            var executiveSummary = BuildCompanyExecutiveSummary(
                company.Name,
                completedRevenue,
                previousCompletedRevenue,
                appointmentTotal,
                previousAppointmentTotal,
                completionRate,
                cancellationRate,
                receivableAmount,
                overdueAmount,
                averageRating,
                newCustomers,
                recurringCustomerCount,
                activeCustomerIds.Count,
                recurringShare);

            return new CompanyReportDto
            {
                GeneratedAtUtc = DateTime.UtcNow,
                CompanyId = company.Id,
                CompanyName = company.Name,
                Period = period,
                Filters = BuildFilterSnapshot(query, period),
                ExecutiveSummary = executiveSummary,
                OverviewCards = new List<ReportKpiCardDto>
                {
                    MakeCard("appointments_total", "Appointments in period", appointmentTotal, FormatInt(appointmentTotal), ChangePct(appointmentTotal, previousAppointmentTotal), "Total appointment volume within the selected period."),
                    MakeCard("completed_rate", "Completion rate", completionRate, FormatPct(completionRate), ChangePct(completionRate, previousCompletionRate), "Percentage of completed appointments out of the total for the period."),
                    MakeCard("revenue_paid", "Revenue collected", completedRevenue, FormatCurrency(completedRevenue), ChangePct(completedRevenue, previousCompletedRevenue), "Only payments marked as paid within the selected period."),
                    MakeCard("customers_active", "Active customers", activeCustomerIds.Count, FormatInt(activeCustomerIds.Count), null, "Customers with at least one appointment in the period."),
                },
                Financial = new CompanyReportFinancialDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Financial",
                        Summary = $"The company generated {FormatCurrency(completedRevenue)} in collected revenue during the period, with an average ticket of {FormatCurrency(averageTicket)} and a collection efficiency of {FormatPct(collectionRate)} over the billed amount.",
                        Highlights = new List<string>
                        {
                            $"Revenue collected changed {FormatSignedPct(ChangePct(completedRevenue, previousCompletedRevenue))} compared with the previous period.",
                            $"Each active customer generated an average of {FormatCurrency(revenuePerActiveCustomer)} in revenue during the analyzed period.",
                            $"There is {FormatCurrency(receivableAmount)} still open, of which {FormatCurrency(overdueAmount)} is already overdue."
                        },
                        Alerts = BuildFinancialAlerts(receivableAmount, overdueAmount, collectionRate, completedRevenue, averageTicket)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("revenue_total", "Revenue collected", completedRevenue, FormatCurrency(completedRevenue), ChangePct(completedRevenue, previousCompletedRevenue), "Payments effectively collected within the selected period."),
                        MakeCard("receivable_amount", "Open balance", receivableAmount, FormatCurrency(receivableAmount), null, "Sum of pending and overdue payments."),
                        MakeCard("average_ticket", "Average ticket", averageTicket, FormatCurrency(averageTicket), null, "Revenue collected divided by the total number of appointments."),
                        MakeCard("collection_rate", "Collection efficiency", collectionRate, FormatPct(collectionRate), null, "Percentage of the billed amount in the period already marked as paid."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Revenue per active customer", Value = FormatCurrency(revenuePerActiveCustomer), Description = "Average revenue generated per active customer during the period." },
                        new() { Label = "Revenue per day", Value = FormatCurrency(period.TotalDays > 0 ? completedRevenue / period.TotalDays : 0m), Description = "Average daily collected revenue." },
                        new() { Label = "Open balance vs. billed amount", Value = FormatPct(totalBilledAmount > 0 ? receivableAmount / totalBilledAmount * 100m : 0m), Description = "Share of the open balance within the billed amount for the period." },
                    },
                    RevenueTrend = BuildDateSeries(period.StartDate, period.EndDate, paidPayments, x => x.PaymentDate ?? x.DueDate, x => x.Amount),
                    PaymentStatusBreakdown = BuildPaymentStatusBreakdown(payments),
                    TopCustomersByRevenue = customerRows.Take(8).ToList(),
                    RecentTransactions = new ReportTableDto
                    {
                        Title = "Recent transactions",
                        Description = "Detailed dataset for the PDF with the most recent receipts and charges in the filtered period.",
                        Columns = new List<ReportTableColumnDto>
                        {
                            new() { Key = "date", Label = "Date" },
                            new() { Key = "reference", Label = "Reference" },
                            new() { Key = "customer", Label = "Customer" },
                            new() { Key = "status", Label = "Status" },
                            new() { Key = "method", Label = "Method" },
                            new() { Key = "amount", Label = "Amount" },
                        },
                        Rows = payments
                            .OrderByDescending(x => x.PaymentDate ?? x.DueDate)
                            .Take(NormalizePageSize(query.PageSize))
                            .Select(x => new ReportTableRowDto
                            {
                                Cells = new Dictionary<string, string>
                                {
                                    ["date"] = FormatDate(x.PaymentDate ?? x.DueDate),
                                    ["reference"] = x.Reference,
                                    ["customer"] = customers.FirstOrDefault(c => c.Id == x.CustomerId)?.Name ?? "No customer",
                                    ["status"] = x.Status.ToString(),
                                    ["method"] = x.Method?.ToString() ?? "Not informed",
                                    ["amount"] = FormatCurrency(x.Amount),
                                }
                            })
                            .ToList(),
                        TotalRows = payments.Count,
                    }
                },
                Operations = new CompanyReportOperationsDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Operations",
                        Summary = $"The operation recorded {FormatInt(appointmentTotal)} appointments during the period, averaging {dailyAverageAppointments.ToString("0.0", CultureInfo.InvariantCulture)} per day, with a completion rate of {FormatPct(completionRate)} and a cancellation rate of {FormatPct(cancellationRate)}.",
                        Highlights = new List<string>
                        {
                            $"The volume changed by {FormatSignedPct(ChangePct(appointmentTotal, previousAppointmentTotal))} compared with the previous period.",
                            $"{FormatPct(recurringShare)} of the analyzed schedule came from recurring appointments.",
                            $"{FormatInt(completedCount)} appointments were completed and {FormatInt(cancelledCount)} were cancelled within the selected interval."
                        },
                        Alerts = BuildOperationsAlerts(cancellationRate, completionRate, recurringShare, dailyAverageAppointments)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("appointments_total", "Appointments", appointmentTotal, FormatInt(appointmentTotal), ChangePct(appointmentTotal, previousAppointmentTotal), "Total operational volume."),
                        MakeCard("completed_total", "Completed", completedCount, FormatInt(completedCount), ChangePct(completedCount, previousCompletedCount), "Appointments successfully completed."),
                        MakeCard("scheduled_total", "Scheduled", scheduledCount, FormatInt(scheduledCount), null, "Appointments still scheduled."),
                        MakeCard("cancellation_rate", "Cancellation rate", cancellationRate, FormatPct(cancellationRate), ChangePct(cancellationRate, previousAppointmentTotal > 0 ? previousCancelledCount / (decimal)previousAppointmentTotal * 100m : 0m), "Share of cancellations out of the total volume for the period."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Daily average appointments", Value = dailyAverageAppointments.ToString("0.0", CultureInfo.InvariantCulture), Description = "Average volume per calendar day in the period." },
                        new() { Label = "Recurring share", Value = FormatPct(recurringShare), Description = "Portion of the schedule generated by recurring services." },
                        new() { Label = "Appointments per active customer", Value = activeCustomerIds.Count > 0 ? (appointmentTotal / (decimal)activeCustomerIds.Count).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Average appointment intensity per active customer." },
                    },
                    AppointmentsTrend = BuildDateSeries(period.StartDate, period.EndDate, appointments, x => x.Start, _ => 1m),
                    StatusBreakdown = BuildStatusBreakdown(appointmentTotal, scheduledCount, inProgressCount, completedCount, cancelledCount),
                    TopServices = serviceRows.Take(8).ToList(),
                    RecentAppointments = new ReportTableDto
                    {
                        Title = "Recent appointments",
                        Description = "Operational dataset ready for PDF export, useful for auditing and detailed service-level review.",
                        Columns = new List<ReportTableColumnDto>
                        {
                            new() { Key = "start", Label = "Date" },
                            new() { Key = "title", Label = "Appointment" },
                            new() { Key = "customer", Label = "Customer" },
                            new() { Key = "service", Label = "Service" },
                            new() { Key = "status", Label = "Status" },
                            new() { Key = "team", Label = "Professionals" },
                        },
                        Rows = appointments
                            .OrderByDescending(x => x.Start)
                            .Take(NormalizePageSize(query.PageSize))
                            .Select(x => new ReportTableRowDto
                            {
                                Cells = new Dictionary<string, string>
                                {
                                    ["start"] = FormatDateTime(x.Start),
                                    ["title"] = x.Title,
                                    ["customer"] = customers.FirstOrDefault(c => c.Id == x.CustomerId)?.Name ?? "No customer",
                                    ["service"] = x.ServiceTypeId.HasValue && serviceTypes.TryGetValue(x.ServiceTypeId.Value, out var serviceName) ? serviceName : (x.Category ?? "No service"),
                                    ["status"] = x.Status.ToString(),
                                    ["team"] = string.Join(", ", professionals.Where(p => x.ProfessionalIds.Contains(p.Id)).Select(p => p.Name).DefaultIfEmpty("Not assigned")),
                                }
                            })
                            .ToList(),
                        TotalRows = appointments.Count,
                    }
                },
                Team = new CompanyReportTeamDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Team",
                        Summary = $"The team had {FormatInt(professionals.Count)} registered professionals, of which {FormatInt(professionals.Count(x => x.Status == StatusEnum.Active))} were active. The consolidated average rating was {averageRating.ToString("0.0", CultureInfo.InvariantCulture)}.",
                        Highlights = new List<string>
                        {
                            $"{FormatInt(teamRows.Count)} professionals actively appeared in the filtered schedule.",
                            $"The average number of completions per engaged professional was {(teamRows.Count > 0 ? teamRows.Average(x => x.PrimaryValue).ToString("0.0", CultureInfo.InvariantCulture) : "0.0")}.",
                            $"Estimated revenue by allocation helps identify operational concentration within the team."
                        },
                        Alerts = BuildTeamAlerts(teamRows, professionals.Count, averageRating)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("professionals_active", "Active professionals", professionals.Count(x => x.Status == StatusEnum.Active), FormatInt(professionals.Count(x => x.Status == StatusEnum.Active)), null, "Professionals marked as active in the registry."),
                        MakeCard("professionals_utilized", "Professionals with schedule", teamRows.Count, FormatInt(teamRows.Count), null, "Professionals who appeared in at least one appointment during the period."),
                        MakeCard("average_rating", "Average rating", averageRating, averageRating.ToString("0.0", CultureInfo.InvariantCulture), null, "Average of the reviews received during the period."),
                        MakeCard("completed_per_professional", "Completions / professional", teamRows.Count > 0 ? teamRows.Average(x => x.PrimaryValue) : 0m, teamRows.Count > 0 ? teamRows.Average(x => x.PrimaryValue).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", null, "Average productivity of engaged professionals."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Team utilization", Value = FormatPct(professionals.Count > 0 ? teamRows.Count / (decimal)professionals.Count * 100m : 0m), Description = "Percentage of registered professionals who had appointments during the period." },
                        new() { Label = "Estimated revenue per professional", Value = FormatCurrency(teamRows.Count > 0 ? teamRows.Average(x => x.SecondaryValue ?? 0m) : 0m), Description = "Estimated average based on the link between appointments and paying customers." },
                        new() { Label = "Leader concentration", Value = FormatPct(teamRows.Any() ? teamRows.First().PrimaryValue / Math.Max(1m, teamRows.Sum(x => x.PrimaryValue)) * 100m : 0m), Description = "Share of the most productive professional in total completions." },
                    },
                    Leaderboard = teamRows.Take(10).ToList(),
                },
                Customers = new CompanyReportCustomersDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Customers",
                        Summary = $"The analyzed base had {FormatInt(newCustomers)} new customers in the period, {FormatInt(activeCustomerIds.Count)} active customers, and {FormatInt(recurringCustomerCount)} recurring customers, indicating the current level of retention and dependency on the existing base.",
                        Highlights = new List<string>
                        {
                            $"Recurring customers represented {FormatPct(activeCustomerIds.Count > 0 ? recurringCustomerCount / (decimal)activeCustomerIds.Count * 100m : 0m)} of active customers.",
                            $"The top 5 customers account for {FormatPct(customerRows.Take(5).Sum(x => x.PrimaryValue) / Math.Max(1m, completedRevenue) * 100m)} of collected revenue.",
                            $"The company served {FormatInt(activeCustomerIds.Count)} different customers in the filtered interval."
                        },
                        Alerts = BuildCustomerAlerts(newCustomers, activeCustomerIds.Count, recurringCustomerCount, completedRevenue, customerRows)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("new_customers", "New customers", newCustomers, FormatInt(newCustomers), null, "Customers created within the selected period."),
                        MakeCard("active_customers", "Active customers", activeCustomerIds.Count, FormatInt(activeCustomerIds.Count), null, "Customers with at least one appointment in the period."),
                        MakeCard("recurring_customers", "Recurring customers", recurringCustomerCount, FormatInt(recurringCustomerCount), null, "Customers with more than one appointment in the period."),
                        MakeCard("avg_revenue_per_customer", "Revenue per active customer", revenuePerActiveCustomer, FormatCurrency(revenuePerActiveCustomer), null, "Revenue collected divided by active customers."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "New over active", Value = FormatPct(activeCustomerIds.Count > 0 ? newCustomers / (decimal)activeCustomerIds.Count * 100m : 0m), Description = "Share of recent acquisition within the active base." },
                        new() { Label = "Base recurrence", Value = FormatPct(activeCustomerIds.Count > 0 ? recurringCustomerCount / (decimal)activeCustomerIds.Count * 100m : 0m), Description = "Share of customers with repeat service." },
                        new() { Label = "Average revenue of top 5", Value = FormatCurrency(customerRows.Take(5).Any() ? customerRows.Take(5).Average(x => x.PrimaryValue) : 0m), Description = "Average ticket value of the top customers in the period." },
                    },
                    TopCustomers = customerRows.Take(10).ToList(),
                    CustomerActivityTable = new ReportTableDto
                    {
                        Title = "Customer activity",
                        Description = "Detailed table for the PDF with appointment frequency and revenue share by customer.",
                        Columns = new List<ReportTableColumnDto>
                        {
                            new() { Key = "customer", Label = "Customer" },
                            new() { Key = "appointments", Label = "Appointments" },
                            new() { Key = "completed", Label = "Completed" },
                            new() { Key = "revenue", Label = "Revenue" },
                            new() { Key = "badge", Label = "Profile" },
                        },
                        Rows = customerRows
                            .Take(NormalizePageSize(query.PageSize))
                            .Select(x => new ReportTableRowDto
                            {
                                Cells = new Dictionary<string, string>
                                {
                                    ["customer"] = x.Name,
                                    ["appointments"] = x.SecondaryValue?.ToString("0", CultureInfo.InvariantCulture) ?? "0",
                                    ["completed"] = appointments.Count(a => a.CustomerId == x.EntityId && a.Status == AppointmentStatus.Completed).ToString(CultureInfo.InvariantCulture),
                                    ["revenue"] = FormatCurrency(x.PrimaryValue),
                                    ["badge"] = x.Badge ?? "One-time",
                                }
                            })
                            .ToList(),
                        TotalRows = customerRows.Count,
                    }
                }
            };
        }

        public async Task<AdminReportDto> GetAdminReportAsync(ReportQueryDto query)
        {
            if (!_currentUser.IsAdmin)
                throw new InvalidOperationException("Use the company endpoint for reports of the logged-in company.");

            var period = BuildPeriod(query);
            var previousQuery = BuildPreviousQuery(query, period);

            var appointments = await GetAppointmentsAsync(null, query);
            var previousAppointments = await GetAppointmentsAsync(null, previousQuery);
            var payments = await GetPaymentsAsync(null, query);
            var previousPayments = await GetPaymentsAsync(null, previousQuery);
            var companies = await _db.Companies.AsNoTracking().ToListAsync();
            var customers = await _db.Customers.AsNoTracking().ToListAsync();
            var professionals = await _db.Professionals.AsNoTracking().ToListAsync();
            var subscriptions = await _db.PlanSubscriptions.AsNoTracking().ToListAsync();

            var paidPayments = payments.Where(x => x.Status == PaymentStatus.Paid).ToList();
            var previousPaidPayments = previousPayments.Where(x => x.Status == PaymentStatus.Paid).ToList();
            var totalRevenue = paidPayments.Sum(x => x.Amount);
            var previousRevenue = previousPaidPayments.Sum(x => x.Amount);
            var totalBilled = payments.Sum(x => x.Amount);
            var overdueAmount = payments.Where(x => x.Status == PaymentStatus.Overdue).Sum(x => x.Amount);
            var collectionRate = totalBilled > 0 ? totalRevenue / totalBilled * 100m : 0m;
            var appointmentTotal = appointments.Count;
            var previousAppointmentTotal = previousAppointments.Count;
            var completedTotal = appointments.Count(x => x.Status == AppointmentStatus.Completed);
            var cancelledTotal = appointments.Count(x => x.Status == AppointmentStatus.Cancelled);
            var scheduledTotal = appointments.Count(x => x.Status == AppointmentStatus.Scheduled);
            var inProgressTotal = appointments.Count(x => x.Status == AppointmentStatus.InProgress);
            var completionRate = appointmentTotal > 0 ? completedTotal / (decimal)appointmentTotal * 100m : 0m;
            var activeCompanies = companies.Count(x => x.Status == StatusEnum.Active);
            var companiesWithAppointments = appointments.Select(x => x.CompanyId).Distinct().Count();
            var activeSubscriptions = subscriptions.Count(x => x.Status == PlanSubscriptionStatusEnum.Active);

            var companyRanking = companies.Select(company =>
            {
                var companyAppointments = appointments.Where(a => a.CompanyId == company.Id).ToList();
                var companyPayments = paidPayments.Where(p => p.CompanyId == company.Id).ToList();
                return new ReportLeaderboardItemDto
                {
                    EntityId = company.Id,
                    Name = company.Name,
                    PrimaryValue = companyPayments.Sum(x => x.Amount),
                    PrimaryLabel = "revenue",
                    SecondaryValue = companyAppointments.Count,
                    SecondaryLabel = "appointments",
                    Badge = company.Status == StatusEnum.Active ? "Active" : company.Status.ToString(),
                };
            })
            .OrderByDescending(x => x.PrimaryValue)
            .ThenByDescending(x => x.SecondaryValue)
            .ToList();

            var executiveSummary = BuildAdminExecutiveSummary(totalRevenue, previousRevenue, activeCompanies, companies.Count, appointmentTotal, previousAppointmentTotal, collectionRate, overdueAmount, activeSubscriptions);

            return new AdminReportDto
            {
                GeneratedAtUtc = DateTime.UtcNow,
                Period = period,
                Filters = BuildFilterSnapshot(query, period),
                ExecutiveSummary = executiveSummary,
                OverviewCards = new List<ReportKpiCardDto>
                {
                    MakeCard("companies_total", "Companies", companies.Count, FormatInt(companies.Count), null, "Total base of registered companies."),
                    MakeCard("companies_active", "Active companies", activeCompanies, FormatInt(activeCompanies), null, "Companies with active status."),
                    MakeCard("appointments_total", "Appointments", appointmentTotal, FormatInt(appointmentTotal), ChangePct(appointmentTotal, previousAppointmentTotal), "Total operational volume in the period."),
                    MakeCard("revenue_paid", "Revenue collected", totalRevenue, FormatCurrency(totalRevenue), ChangePct(totalRevenue, previousRevenue), "Revenue effectively paid during the period."),
                },
                Billing = new AdminReportBillingDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Billing",
                        Summary = $"The platform recorded {FormatCurrency(totalRevenue)} in collected revenue, with a collection efficiency of {FormatPct(collectionRate)} and {FormatCurrency(overdueAmount)} in overdue amounts during the filtered period.",
                        Highlights = new List<string>
                        {
                            $"There are {FormatInt(activeSubscriptions)} active subscriptions in the base.",
                            $"{FormatInt(companiesWithAppointments)} companies had operational usage during the period.",
                            $"Revenue changed by {FormatSignedPct(ChangePct(totalRevenue, previousRevenue))} compared with the previous period."
                        },
                        Alerts = BuildAdminBillingAlerts(overdueAmount, collectionRate, activeSubscriptions, activeCompanies)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("subscriptions_active", "Active subscriptions", activeSubscriptions, FormatInt(activeSubscriptions), null, "Subscriptions with active status."),
                        MakeCard("companies_with_usage", "Companies with usage", companiesWithAppointments, FormatInt(companiesWithAppointments), null, "Companies with at least one appointment in the period."),
                        MakeCard("overdue_amount", "Overdue amount", overdueAmount, FormatCurrency(overdueAmount), null, "Overdue charges during the period."),
                        MakeCard("collection_rate", "Collection efficiency", collectionRate, FormatPct(collectionRate), null, "Paid revenue over the total billed amount in the period."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Revenue per active company", Value = FormatCurrency(activeCompanies > 0 ? totalRevenue / activeCompanies : 0m), Description = "Average monetization per active company." },
                        new() { Label = "Operational usage of the base", Value = FormatPct(companies.Count > 0 ? companiesWithAppointments / (decimal)companies.Count * 100m : 0m), Description = "Percentage of the base with operational activity during the period." },
                        new() { Label = "Overdue over billed", Value = FormatPct(totalBilled > 0 ? overdueAmount / totalBilled * 100m : 0m), Description = "Share of overdue balance within the billed amount for the period." },
                    },
                    RevenueTrend = BuildDateSeries(period.StartDate, period.EndDate, paidPayments, x => x.PaymentDate ?? x.DueDate, x => x.Amount),
                    PaymentStatusBreakdown = BuildPaymentStatusBreakdown(payments),
                    CompaniesWithPaymentRisk = payments
                        .Where(x => x.Status == PaymentStatus.Overdue)
                        .GroupBy(x => x.CompanyId)
                        .Select(g => new ReportLeaderboardItemDto
                        {
                            EntityId = g.Key,
                            Name = companies.FirstOrDefault(c => c.Id == g.Key)?.Name ?? $"Company {g.Key}",
                            PrimaryValue = g.Sum(x => x.Amount),
                            PrimaryLabel = "overdue",
                            SecondaryValue = g.Count(),
                            SecondaryLabel = "charges",
                            Badge = "Attention",
                        })
                        .OrderByDescending(x => x.PrimaryValue)
                        .Take(10)
                        .ToList(),
                },
                Operations = new AdminReportOperationsDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Operations",
                        Summary = $"The platform's consolidated operation recorded {FormatInt(appointmentTotal)} appointments, with a completion rate of {FormatPct(completionRate)} and a cancellation rate of {FormatPct(appointmentTotal > 0 ? cancelledTotal / (decimal)appointmentTotal * 100m : 0m)}.",
                        Highlights = new List<string>
                        {
                            $"The total base has {FormatInt(customers.Count)} customers and {FormatInt(professionals.Count)} registered professionals.",
                            $"Operational volume changed by {FormatSignedPct(ChangePct(appointmentTotal, previousAppointmentTotal))} compared with the previous period.",
                            $"Status monitoring shows a balance between scheduled, in-progress, and completed appointments, which is useful for the executive PDF reading."
                        },
                        Alerts = BuildAdminOperationsAlerts(completionRate, appointmentTotal, cancelledTotal, companiesWithAppointments, companies.Count)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("completion_rate", "Completion rate", completionRate, FormatPct(completionRate), null, "Completed appointments over total appointments."),
                        MakeCard("completed_total", "Completed", completedTotal, FormatInt(completedTotal), null, "Appointments successfully completed."),
                        MakeCard("customers_total", "Customers", customers.Count, FormatInt(customers.Count), null, "Total customers in the base."),
                        MakeCard("professionals_total", "Professionals", professionals.Count, FormatInt(professionals.Count), null, "Total professionals in the base."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Appointments per company with usage", Value = companiesWithAppointments > 0 ? (appointmentTotal / (decimal)companiesWithAppointments).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Average usage intensity per operationally active company." },
                        new() { Label = "Customers per company", Value = companies.Count > 0 ? (customers.Count / (decimal)companies.Count).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Average customer-base scale per company." },
                        new() { Label = "Professionals per company", Value = companies.Count > 0 ? (professionals.Count / (decimal)companies.Count).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Average team capacity per company." },
                    },
                    AppointmentsTrend = BuildDateSeries(period.StartDate, period.EndDate, appointments, x => x.Start, _ => 1m),
                    StatusBreakdown = BuildStatusBreakdown(appointmentTotal, scheduledTotal, inProgressTotal, completedTotal, cancelledTotal),
                },
                Companies = new AdminReportCompaniesDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Companies",
                        Summary = $"The consolidated ranking shows which companies drive revenue and operational volume, helping the front end generate an executive PDF with tenant comparison, result concentration, and financial risk exposure.",
                        Highlights = new List<string>
                        {
                            $"The top 5 companies account for {FormatPct(companyRanking.Take(5).Sum(x => x.PrimaryValue) / Math.Max(1m, totalRevenue) * 100m)} of collected revenue.",
                            $"{FormatInt(activeCompanies)} companies are active within a total base of {FormatInt(companies.Count)} companies.",
                            $"The ranking combines revenue and operational volume to avoid a one-dimensional reading."
                        },
                        Alerts = BuildAdminCompanyAlerts(companyRanking, totalRevenue, activeCompanies, companies.Count)
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Average revenue top 5", Value = FormatCurrency(companyRanking.Take(5).Any() ? companyRanking.Take(5).Average(x => x.PrimaryValue) : 0m), Description = "Average revenue among the leaders of the base." },
                        new() { Label = "Overall average revenue", Value = FormatCurrency(companies.Count > 0 ? totalRevenue / companies.Count : 0m), Description = "Average revenue distribution per registered company." },
                        new() { Label = "Share of active companies", Value = FormatPct(companies.Count > 0 ? activeCompanies / (decimal)companies.Count * 100m : 0m), Description = "Share of active companies over the total base." },
                    },
                    Ranking = companyRanking.Take(10).ToList(),
                    CompaniesTable = new ReportTableDto
                    {
                        Title = "Company ranking",
                        Description = "Consolidated table for the administrative PDF and platform company comparison.",
                        Columns = new List<ReportTableColumnDto>
                        {
                            new() { Key = "company", Label = "Company" },
                            new() { Key = "status", Label = "Status" },
                            new() { Key = "revenue", Label = "Revenue" },
                            new() { Key = "appointments", Label = "Appointments" },
                            new() { Key = "customers", Label = "Customers" },
                            new() { Key = "professionals", Label = "Professionals" },
                        },
                        Rows = companyRanking
                            .Take(NormalizePageSize(query.PageSize))
                            .Select(rank =>
                            {
                                var company = companies.FirstOrDefault(c => c.Id == rank.EntityId);
                                var companyCustomers = customers.Count(c => c.CompanyId == rank.EntityId);
                                var companyProfessionals = professionals.Count(p => p.CompanyId == rank.EntityId);
                                return new ReportTableRowDto
                                {
                                    Cells = new Dictionary<string, string>
                                    {
                                        ["company"] = rank.Name,
                                        ["status"] = company?.Status.ToString() ?? "Unknown",
                                        ["revenue"] = FormatCurrency(rank.PrimaryValue),
                                        ["appointments"] = rank.SecondaryValue?.ToString("0", CultureInfo.InvariantCulture) ?? "0",
                                        ["customers"] = companyCustomers.ToString(CultureInfo.InvariantCulture),
                                        ["professionals"] = companyProfessionals.ToString(CultureInfo.InvariantCulture),
                                    }
                                };
                            })
                            .ToList(),
                        TotalRows = companyRanking.Count,
                    }
                }
            };
        }

        public async Task<byte[]> ExportCompanyReportCsvAsync(ReportQueryDto query)
        {
            var report = await GetCompanyReportAsync(query);
            var sb = new StringBuilder();
            sb.AppendLine("Secao,Indicador,Valor");
            foreach (var card in report.OverviewCards)
                sb.AppendLine($"Overview,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            foreach (var card in report.Financial.Cards)
                sb.AppendLine($"Financial,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            foreach (var card in report.Operations.Cards)
                sb.AppendLine($"Operacoes,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            foreach (var card in report.Team.Cards)
                sb.AppendLine($"Equipe,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            foreach (var card in report.Customers.Cards)
                sb.AppendLine($"Customers,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public async Task<byte[]> ExportAdminReportCsvAsync(ReportQueryDto query)
        {
            var report = await GetAdminReportAsync(query);
            var sb = new StringBuilder();
            sb.AppendLine("Secao,Indicador,Valor");
            foreach (var card in report.OverviewCards)
                sb.AppendLine($"Overview,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            foreach (var card in report.Billing.Cards)
                sb.AppendLine($"Billing,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            foreach (var card in report.Operations.Cards)
                sb.AppendLine($"Operacoes,{Escape(card.Label)},{Escape(card.DisplayValue)}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private async Task<int> ResolveCompanyIdAsync()
        {
            if (_currentUser.IsAdmin)
                throw new InvalidOperationException("Use the admin endpoint for global reports.");

            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new InvalidOperationException("Company scope not found.");

            return scopedCompanyId.Value;
        }

        private async Task<List<Appointment>> GetAppointmentsAsync(int? companyId, ReportQueryDto query)
        {
            var period = BuildPeriod(query);
            var statusFilter = TryParseStatus(query.Status);

            var normalAppointmentsQuery = _db.Appointments.AsNoTracking()
                .Where(x => !x.IsRecurring && x.Start < period.EndDate && x.End > period.StartDate);

            if (companyId.HasValue)
                normalAppointmentsQuery = normalAppointmentsQuery.Where(x => x.CompanyId == companyId.Value);
            if (query.CustomerId.HasValue)
                normalAppointmentsQuery = normalAppointmentsQuery.Where(x => x.CustomerId == query.CustomerId.Value);
            if (query.ServiceTypeId.HasValue)
                normalAppointmentsQuery = normalAppointmentsQuery.Where(x => x.ServiceTypeId == query.ServiceTypeId.Value);
            if (statusFilter.HasValue)
                normalAppointmentsQuery = normalAppointmentsQuery.Where(x => x.Status == statusFilter.Value);

            var normalAppointments = await normalAppointmentsQuery.ToListAsync();

            if (query.ProfessionalId.HasValue)
            {
                var professionalId = query.ProfessionalId.Value;
                normalAppointments = normalAppointments
                    .Where(x => x.ProfessionalIds.Contains(professionalId))
                    .ToList();
            }

            var recurringAnchorsQuery = _db.Appointments.AsNoTracking()
                .Where(x => x.IsRecurring
                         && x.SeriesId != null
                         && !string.IsNullOrWhiteSpace(x.RecurrenceRule)
                         && x.Start <= period.EndDate
                         && (!x.RecurrenceEnd.HasValue || x.RecurrenceEnd.Value >= period.StartDate));

            if (companyId.HasValue)
                recurringAnchorsQuery = recurringAnchorsQuery.Where(x => x.CompanyId == companyId.Value);
            if (query.CustomerId.HasValue)
                recurringAnchorsQuery = recurringAnchorsQuery.Where(x => x.CustomerId == query.CustomerId.Value);
            if (query.ServiceTypeId.HasValue)
                recurringAnchorsQuery = recurringAnchorsQuery.Where(x => x.ServiceTypeId == query.ServiceTypeId.Value);

            var recurringAnchors = await recurringAnchorsQuery.ToListAsync();
            var recurringOccurrences = await ExpandRecurringAppointmentsAsync(recurringAnchors, period.StartDate, period.EndDate, query.ProfessionalId, query.ServiceTypeId, statusFilter);

            return normalAppointments
                .Concat(recurringOccurrences)
                .OrderBy(x => x.Start)
                .ThenBy(x => x.Id)
                .ToList();
        }


        private async Task<List<Appointment>> ExpandRecurringAppointmentsAsync(
            List<Appointment> anchors,
            DateTime rangeStart,
            DateTime rangeEnd,
            int? professionalId,
            int? serviceTypeId,
            AppointmentStatus? statusFilter)
        {
            if (anchors.Count == 0)
                return new List<Appointment>();

            var seriesIds = anchors
                .Where(x => x.SeriesId.HasValue)
                .Select(x => x.SeriesId!.Value)
                .Distinct()
                .ToList();

            if (seriesIds.Count == 0)
                return new List<Appointment>();

            var exceptions = await _db.Set<AppointmentRecurrenceException>().AsNoTracking()
                .Where(e => seriesIds.Contains(e.SeriesId)
                         && e.OccurrenceStart < rangeEnd
                         && e.OccurrenceEnd > rangeStart)
                .OrderBy(e => e.SeriesId)
                .ThenBy(e => e.OccurrenceStart)
                .ThenByDescending(e => e.UpdatedDate)
                .ToListAsync();

            var exceptionMap = exceptions
                .GroupBy(e => (e.SeriesId, e.OccurrenceStart))
                .ToDictionary(g => g.Key, g => g.First());

            var completionMap = await _db.AppointmentCompletions.AsNoTracking()
                .Where(c => seriesIds.Contains(c.SeriesId ?? Guid.Empty)
                         && c.OccurrenceStart < rangeEnd
                         && c.OccurrenceEnd > rangeStart)
                .ToDictionaryAsync(c => (c.AppointmentId, c.OccurrenceStart), c => c);

            var occurrences = new List<Appointment>();

            foreach (var anchor in anchors)
            {
                if (!anchor.SeriesId.HasValue || string.IsNullOrWhiteSpace(anchor.RecurrenceRule))
                    continue;

                var limit = anchor.RecurrenceEnd.HasValue && anchor.RecurrenceEnd.Value < rangeEnd
                    ? anchor.RecurrenceEnd.Value
                    : rangeEnd;

                var expandedWindows = ExpandOccurrences(
                    anchor.RecurrenceRule!,
                    anchor.Start,
                    anchor.End,
                    limit,
                    anchor.OccurrenceCount);

                foreach (var (occurrenceStart, occurrenceEnd) in expandedWindows)
                {
                    if (occurrenceStart >= rangeEnd || occurrenceEnd <= rangeStart)
                        continue;

                    exceptionMap.TryGetValue((anchor.SeriesId.Value, occurrenceStart), out var ex);
                    if (ex?.IsCancelled == true)
                        continue;

                    completionMap.TryGetValue((anchor.Id, occurrenceStart), out var completion);

                    var merged = CloneOccurrence(anchor, occurrenceStart, occurrenceEnd, ex, completion);

                    if (professionalId.HasValue && !merged.ProfessionalIds.Contains(professionalId.Value))
                        continue;
                    if (serviceTypeId.HasValue && merged.ServiceTypeId != serviceTypeId.Value)
                        continue;
                    if (statusFilter.HasValue && merged.Status != statusFilter.Value)
                        continue;

                    occurrences.Add(merged);
                }
            }

            return occurrences;
        }

        private static Appointment CloneOccurrence(
            Appointment anchor,
            DateTime occurrenceStart,
            DateTime occurrenceEnd,
            AppointmentRecurrenceException? exception,
            AppointmentCompletion? completion)
        {
            var start = exception?.OverrideStart ?? occurrenceStart;
            var end = exception?.OverrideEnd ?? occurrenceEnd;
            var professionalIds = completion?.ProfessionalIdsSnapshot?.Distinct().ToList()
                ?? ((exception?.OverrideProfessionalIds != null && exception.OverrideProfessionalIds.Any())
                    ? exception.OverrideProfessionalIds.Distinct().ToList()
                    : anchor.ProfessionalIds.Distinct().ToList());

            var status = completion != null
                ? AppointmentStatus.Completed
                : exception?.OverrideStatus ?? anchor.Status;

            var type = exception?.OverrideType ?? anchor.Type;
            var category = anchor.Category;
            if (string.IsNullOrWhiteSpace(category))
                category = type.ToString();

            return new Appointment
            {
                Id = anchor.Id,
                Title = exception?.OverrideTitle ?? anchor.Title,
                Address = exception?.OverrideAddress ?? anchor.Address,
                Start = start,
                End = end,
                CompanyId = anchor.CompanyId,
                CustomerId = completion?.CustomerIdSnapshot ?? anchor.CustomerId,
                CustomerAddressId = completion?.CustomerAddressIdSnapshot ?? exception?.OverrideCustomerAddressId ?? anchor.CustomerAddressId,
                TeamId = completion?.TeamIdSnapshot ?? anchor.TeamId,
                ProfessionalIds = professionalIds,
                Status = status,
                Type = type,
                Category = category,
                ServiceTypeId = completion?.ServiceTypeIdSnapshot ?? exception?.OverrideServiceTypeId ?? anchor.ServiceTypeId,
                Notes = exception?.OverrideNotes ?? anchor.Notes,
                TimeZoneId = anchor.TimeZoneId,
                IsRecurring = true,
                RecurrenceRule = anchor.RecurrenceRule,
                SeriesId = anchor.SeriesId,
                RecurrenceEnd = anchor.RecurrenceEnd,
                OccurrenceCount = anchor.OccurrenceCount,
                IsException = exception != null,
                OriginalStart = occurrenceStart,
                OriginalEnd = occurrenceEnd,
                ExternalSource = anchor.ExternalSource,
                ExternalReservationId = anchor.ExternalReservationId,
                ExternalListingId = anchor.ExternalListingId,
                ExternalStatus = anchor.ExternalStatus
            };
        }

        private static AppointmentStatus? TryParseStatus(string? rawStatus)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
                return null;

            return Enum.TryParse<AppointmentStatus>(rawStatus, true, out var parsed)
                ? parsed
                : null;
        }

        private async Task<List<Core.Models.Payment>> GetPaymentsAsync(int? companyId, ReportQueryDto query)
        {
            var period = BuildPeriod(query);
            var paymentsQuery = _db.Payments.AsNoTracking()
                .Where(x => (x.PaymentDate ?? x.DueDate) >= period.StartDate && (x.PaymentDate ?? x.DueDate) <= period.EndDate);

            if (companyId.HasValue)
                paymentsQuery = paymentsQuery.Where(x => x.CompanyId == companyId.Value);
            if (query.CustomerId.HasValue)
                paymentsQuery = paymentsQuery.Where(x => x.CustomerId == query.CustomerId.Value);

            return await paymentsQuery.ToListAsync();
        }

        private static ReportQueryDto BuildPreviousQuery(ReportQueryDto query, ReportPeriodDto period)
        {
            return new ReportQueryDto
            {
                StartDate = period.PreviousStartDate,
                EndDate = period.PreviousEndDate,
                ProfessionalId = query.ProfessionalId,
                CustomerId = query.CustomerId,
                ServiceTypeId = query.ServiceTypeId,
                Status = query.Status,
                Page = query.Page,
                PageSize = query.PageSize,
            };
        }

        private static ReportPeriodDto BuildPeriod(ReportQueryDto query)
        {
            var end = (query.EndDate ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
            var start = (query.StartDate ?? end.Date.AddDays(-29)).Date;
            if (start > end)
            {
                var temp = start;
                start = end.Date;
                end = temp.AddDays(1).AddTicks(-1);
            }

            var totalDays = Math.Max(1, (int)Math.Ceiling((end - start).TotalDays) + 1);
            var previousEnd = start.AddTicks(-1);
            var previousStart = start.AddDays(-totalDays);

            return new ReportPeriodDto
            {
                StartDate = start,
                EndDate = end,
                PreviousStartDate = previousStart,
                PreviousEndDate = previousEnd,
                TotalDays = totalDays,
            };
        }

        private static ReportFilterSnapshotDto BuildFilterSnapshot(ReportQueryDto query, ReportPeriodDto period)
        {
            var activeFilters = new List<string>
            {
                $"Period: {period.StartDate:MM/dd/yyyy} to {period.EndDate:MM/dd/yyyy}"
            };

            if (query.ProfessionalId.HasValue)
                activeFilters.Add($"ProfessionalId: {query.ProfessionalId.Value}");
            if (query.CustomerId.HasValue)
                activeFilters.Add($"CustomerId: {query.CustomerId.Value}");
            if (query.ServiceTypeId.HasValue)
                activeFilters.Add($"ServiceTypeId: {query.ServiceTypeId.Value}");
            if (!string.IsNullOrWhiteSpace(query.Status))
                activeFilters.Add($"Status: {query.Status}");

            return new ReportFilterSnapshotDto
            {
                StartDate = query.StartDate ?? period.StartDate,
                EndDate = query.EndDate ?? period.EndDate,
                ProfessionalId = query.ProfessionalId,
                CustomerId = query.CustomerId,
                ServiceTypeId = query.ServiceTypeId,
                Status = query.Status,
                DisplayPeriod = $"{period.StartDate:MM/dd/yyyy} - {period.EndDate:MM/dd/yyyy}",
                ActiveFilters = activeFilters,
            };
        }

        private static List<ReportSeriesPointDto> BuildDateSeries<T>(DateTime start, DateTime end, IEnumerable<T> source, Func<T, DateTime> dateSelector, Func<T, decimal> valueSelector)
        {
            var days = Enumerable.Range(0, Math.Max(1, (end.Date - start.Date).Days + 1))
                .Select(offset => start.Date.AddDays(offset))
                .ToList();

            var groups = source
                .GroupBy(item => dateSelector(item).Date)
                .ToDictionary(g => g.Key, g => g.Sum(valueSelector));

            return days.Select(day => new ReportSeriesPointDto
            {
                Label = day.ToString("dd/MM"),
                Value = groups.TryGetValue(day, out var value) ? value : 0m,
            }).ToList();
        }

        private static List<ReportBreakdownItemDto> BuildStatusBreakdown(int total, int scheduled, int inProgress, int completed, int cancelled)
        {
            var items = new[]
            {
                new { Key = "scheduled", Label = "Scheduled", Value = (decimal)scheduled },
                new { Key = "in_progress", Label = "In Progress", Value = (decimal)inProgress },
                new { Key = "completed", Label = "Completed", Value = (decimal)completed },
                new { Key = "cancelled", Label = "Cancelled", Value = (decimal)cancelled },
            };

            return items.Select(item => new ReportBreakdownItemDto
            {
                Key = item.Key,
                Label = item.Label,
                Value = item.Value,
                Percentage = total > 0 ? Math.Round(item.Value / total * 100m, 2) : 0m,
            }).ToList();
        }

        private static List<ReportBreakdownItemDto> BuildPaymentStatusBreakdown(List<Core.Models.Payment> payments)
        {
            var total = payments.Sum(x => x.Amount);
            return payments
                .GroupBy(x => x.Status)
                .Select(g => new ReportBreakdownItemDto
                {
                    Key = g.Key.ToString().ToLowerInvariant(),
                    Label = g.Key.ToString(),
                    Value = g.Sum(x => x.Amount),
                    Percentage = total > 0 ? Math.Round(g.Sum(x => x.Amount) / total * 100m, 2) : 0m,
                })
                .OrderByDescending(x => x.Value)
                .ToList();
        }

        private static List<ReportLeaderboardItemDto> BuildProfessionalRows(
            List<Core.Models.Appointment> appointments,
            List<Core.Models.Payment> paidPayments,
            List<Core.Models.Professional> professionals,
            List<Core.Models.Review> reviews)
        {
            var revenueByCustomer = paidPayments
                .Where(x => x.CustomerId.HasValue)
                .GroupBy(x => x.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var rows = new List<ReportLeaderboardItemDto>();
            foreach (var professional in professionals)
            {
                var professionalAppointments = appointments.Where(a => a.ProfessionalIds.Contains(professional.Id)).ToList();
                if (!professionalAppointments.Any())
                    continue;

                var relatedCustomerIds = professionalAppointments.Where(a => a.CustomerId.HasValue).Select(a => a.CustomerId!.Value).Distinct();
                var estimatedRevenue = relatedCustomerIds.Sum(customerId => revenueByCustomer.TryGetValue(customerId, out var value) ? value : 0m);
                var professionalReviews = reviews.Where(r => r.ProfessionalId == professional.Id).ToList();
                var rating = professionalReviews.Any() ? professionalReviews.Average(r => r.Rating) : (professional.Rating ?? 0d);

                rows.Add(new ReportLeaderboardItemDto
                {
                    EntityId = professional.Id,
                    Name = professional.Name,
                    PrimaryValue = professionalAppointments.Count(a => a.Status == AppointmentStatus.Completed),
                    PrimaryLabel = "completed",
                    SecondaryValue = estimatedRevenue,
                    SecondaryLabel = "estimated revenue",
                    Badge = rating > 0 ? $"{rating:0.0}★" : null,
                });
            }

            return rows.OrderByDescending(x => x.PrimaryValue).ThenByDescending(x => x.SecondaryValue).ToList();
        }

        private static List<ReportLeaderboardItemDto> BuildCustomerRevenueRows(
            List<Core.Models.Appointment> appointments,
            List<Core.Models.Payment> paidPayments,
            List<Core.Models.Customer> customers)
        {
            var paymentsByCustomer = paidPayments
                .Where(x => x.CustomerId.HasValue)
                .GroupBy(x => x.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var appointmentsByCustomer = appointments
                .Where(x => x.CustomerId.HasValue)
                .GroupBy(x => x.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            return customers
                .Where(c => appointmentsByCustomer.ContainsKey(c.Id) || paymentsByCustomer.ContainsKey(c.Id))
                .Select(c =>
                {
                    appointmentsByCustomer.TryGetValue(c.Id, out var customerAppointments);
                    var appts = customerAppointments ?? new List<Core.Models.Appointment>();
                    var revenue = paymentsByCustomer.TryGetValue(c.Id, out var amount) ? amount : 0m;
                    return new ReportLeaderboardItemDto
                    {
                        EntityId = c.Id,
                        Name = c.Name,
                        PrimaryValue = revenue,
                        PrimaryLabel = "revenue",
                        SecondaryValue = appts.Count,
                        SecondaryLabel = "appointments",
                        Badge = appts.Count > 1 ? "Recurring" : "One-time",
                    };
                })
                .OrderByDescending(x => x.PrimaryValue)
                .ThenByDescending(x => x.SecondaryValue)
                .ToList();
        }

        private static ReportExecutiveSummaryDto BuildCompanyExecutiveSummary(
            string companyName,
            decimal revenue,
            decimal previousRevenue,
            int appointments,
            int previousAppointments,
            decimal completionRate,
            decimal cancellationRate,
            decimal receivableAmount,
            decimal overdueAmount,
            decimal averageRating,
            int newCustomers,
            int recurringCustomers,
            int activeCustomers,
            decimal recurringShare)
        {
            var strengths = new List<string>();
            var risks = new List<string>();
            var recommendedActions = new List<string>();

            if (ChangePct(revenue, previousRevenue) >= 0)
                strengths.Add($"Collected revenue remained on a positive trajectory, changing by {FormatSignedPct(ChangePct(revenue, previousRevenue))} compared with the previous period.");
            if (completionRate >= 80m)
                strengths.Add($"A completion rate of {FormatPct(completionRate)} indicates healthy operational execution.");
            if (averageRating >= 4m)
                strengths.Add($"Customer perception was favorable, with an average rating of {averageRating.ToString("0.0", CultureInfo.InvariantCulture)}.");
            if (recurringCustomers > 0)
                strengths.Add($"The customer base shows active retention, with {FormatInt(recurringCustomers)} recurring customers during the period.");

            if (cancellationRate >= 15m)
                risks.Add($"A cancellation rate of {FormatPct(cancellationRate)} deserves investigation into operational and commercial causes.");
            if (overdueAmount > 0)
                risks.Add($"There is {FormatCurrency(overdueAmount)} in overdue amounts, which puts pressure on cash flow and reduces predictability.");
            if (receivableAmount > revenue && receivableAmount > 0)
                risks.Add("Open balance already exceeds collected revenue for the period, signaling collection risk.");
            if (activeCustomers == 0)
                risks.Add("There were no active customers in the filtered period, which may indicate an overly restrictive filter or low operational activity.");

            recommendedActions.Add("Use the PDF to compare revenue, cancellations, and retention across upcoming periods and measure trends, not just an isolated snapshot.");
            if (overdueAmount > 0)
                recommendedActions.Add("Prioritize a collection workflow to reduce overdue balances and improve the conversion of billed amounts into real cash.");
            if (cancellationRate >= 10m)
                recommendedActions.Add("Analyze cancellation reasons by service, customer, and professional to address the true bottleneck.");
            if (newCustomers > 0 && recurringCustomers < newCustomers)
                recommendedActions.Add("Create a retention action to convert recent acquisition into real recurrence.");

            var healthStatus = "neutral";
            if (completionRate >= 80m && cancellationRate < 10m && overdueAmount <= 0)
                healthStatus = "good";
            else if (cancellationRate >= 15m || overdueAmount > 0)
                healthStatus = "attention";

            return new ReportExecutiveSummaryDto
            {
                Headline = $"Executive Summary — {companyName}",
                HealthStatus = healthStatus,
                Narrative = $"During the analyzed period, the company processed {FormatInt(appointments)} appointments and {FormatCurrency(revenue)} in collected revenue. Operations closed with a completion rate of {FormatPct(completionRate)}, a cancellation rate of {FormatPct(cancellationRate)}, and a recurring share of {FormatPct(recurringShare)}.",
                Strengths = strengths.Take(4).ToList(),
                Risks = risks.Take(4).ToList(),
                RecommendedActions = recommendedActions.Take(4).ToList(),
            };
        }

        private static ReportExecutiveSummaryDto BuildAdminExecutiveSummary(decimal revenue, decimal previousRevenue, int activeCompanies, int totalCompanies, int appointments, int previousAppointments, decimal collectionRate, decimal overdueAmount, int activeSubscriptions)
        {
            var strengths = new List<string>();
            var risks = new List<string>();
            var recommendedActions = new List<string>();

            strengths.Add($"The platform has {FormatInt(activeCompanies)} active companies within a base of {FormatInt(totalCompanies)} companies.");
            strengths.Add($"There were {FormatInt(activeSubscriptions)} active subscriptions, supporting the monetization view of the base.");
            if (ChangePct(revenue, previousRevenue) >= 0)
                strengths.Add($"Collected revenue changed by {FormatSignedPct(ChangePct(revenue, previousRevenue))} compared with the previous period.");

            if (overdueAmount > 0)
                risks.Add($"The base holds {FormatCurrency(overdueAmount)} in overdue amounts, which requires collection follow-up.");
            if (collectionRate < 70m)
                risks.Add($"Collection efficiency is at {FormatPct(collectionRate)}, below the ideal level for cash-flow predictability.");
            if (ChangePct(appointments, previousAppointments) < 0)
                risks.Add("Operational volume declined compared with the previous period and may indicate reduced usage across part of the base.");

            recommendedActions.Add("Use the administrative PDF to highlight leading companies, delinquency risk, and platform usage density.");
            recommendedActions.Add("Cross-reference companies with higher overdue balances against those with lower usage to identify churn and collection risk.");
            recommendedActions.Add("Monitor revenue evolution per active company to distinguish healthy growth from excessive concentration.");

            var healthStatus = overdueAmount > 0 || collectionRate < 70m ? "attention" : "good";

            return new ReportExecutiveSummaryDto
            {
                Headline = "Executive Summary — Platform",
                HealthStatus = healthStatus,
                Narrative = $"During the analyzed period, the platform processed {FormatInt(appointments)} appointments and {FormatCurrency(revenue)} in collected revenue, with a collection efficiency of {FormatPct(collectionRate)}.",
                Strengths = strengths.Take(4).ToList(),
                Risks = risks.Take(4).ToList(),
                RecommendedActions = recommendedActions.Take(4).ToList(),
            };
        }

        private static List<string> BuildFinancialAlerts(decimal receivableAmount, decimal overdueAmount, decimal collectionRate, decimal revenue, decimal averageTicket)
        {
            var alerts = new List<string>();
            if (overdueAmount > 0)
                alerts.Add($"Overdue balance identified: {FormatCurrency(overdueAmount)}.");
            if (collectionRate < 70m)
                alerts.Add($"Collection efficiency below the ideal level: {FormatPct(collectionRate)}.");
            if (receivableAmount > revenue && receivableAmount > 0)
                alerts.Add("Open balance already exceeds collected revenue for the period.");
            if (averageTicket <= 0)
                alerts.Add("There is no calculable average ticket for the filtered data.");
            return alerts;
        }

        private static List<string> BuildOperationsAlerts(decimal cancellationRate, decimal completionRate, decimal recurringShare, decimal dailyAverageAppointments)
        {
            var alerts = new List<string>();
            if (cancellationRate >= 15m)
                alerts.Add("Cancellation is high for the analyzed period.");
            if (completionRate < 70m)
                alerts.Add("Completion rate is below 70%, indicating room for operational adjustment.");
            if (recurringShare < 20m)
                alerts.Add("Recurring share is low; the schedule depends more on one-time demand.");
            if (dailyAverageAppointments < 1m)
                alerts.Add("Operational density per day is low within the filtered period.");
            return alerts;
        }

        private static List<string> BuildTeamAlerts(List<ReportLeaderboardItemDto> teamRows, int totalProfessionals, decimal averageRating)
        {
            var alerts = new List<string>();
            if (teamRows.Count < totalProfessionals && totalProfessionals > 0)
                alerts.Add("Part of the registered team did not appear in the schedule for the period, which may signal idleness or an overly restrictive filter.");
            if (teamRows.Any() && teamRows.First().PrimaryValue > Math.Max(1m, teamRows.Sum(x => x.PrimaryValue)) * 0.4m)
                alerts.Add("Productivity is concentrated among a few professionals, increasing operational dependency.");
            if (averageRating > 0 && averageRating < 4m)
                alerts.Add("Average rating is below 4.0; investigate feedback and customer experience.");
            return alerts;
        }

        private static List<string> BuildCustomerAlerts(int newCustomers, int activeCustomers, int recurringCustomers, decimal revenue, List<ReportLeaderboardItemDto> customerRows)
        {
            var alerts = new List<string>();
            if (activeCustomers > 0 && recurringCustomers < activeCustomers * 0.3m)
                alerts.Add("Recurrence is low relative to the active customer base.");
            if (customerRows.Any() && customerRows.Take(3).Sum(x => x.PrimaryValue) > Math.Max(1m, revenue) * 0.6m)
                alerts.Add("Revenue is concentrated among a few customers; watch for dependency risk.");
            if (newCustomers == 0)
                alerts.Add("No new customer entered the base during the filtered period.");
            return alerts;
        }

        private static List<string> BuildAdminBillingAlerts(decimal overdueAmount, decimal collectionRate, int activeSubscriptions, int activeCompanies)
        {
            var alerts = new List<string>();
            if (overdueAmount > 0)
                alerts.Add("There are overdue charges in the base and they should be on the finance team's radar.");
            if (collectionRate < 70m)
                alerts.Add("Collection efficiency is below the ideal level for a predictable base.");
            if (activeSubscriptions < activeCompanies)
                alerts.Add("Not every active company has an active subscription; review commercial adoption and contract status.");
            return alerts;
        }

        private static List<string> BuildAdminOperationsAlerts(decimal completionRate, int appointmentTotal, int cancelledTotal, int companiesWithAppointments, int totalCompanies)
        {
            var alerts = new List<string>();
            if (completionRate < 75m)
                alerts.Add("The platform's global completion rate is below the desired level.");
            if (appointmentTotal > 0 && cancelledTotal / (decimal)appointmentTotal >= 0.15m)
                alerts.Add("The platform's consolidated cancellation rate is high.");
            if (totalCompanies > 0 && companiesWithAppointments / (decimal)totalCompanies < 0.5m)
                alerts.Add("Less than half of the base had operational usage during the period.");
            return alerts;
        }

        private static List<string> BuildAdminCompanyAlerts(List<ReportLeaderboardItemDto> companyRanking, decimal totalRevenue, int activeCompanies, int totalCompanies)
        {
            var alerts = new List<string>();
            if (companyRanking.Any() && companyRanking.Take(3).Sum(x => x.PrimaryValue) > Math.Max(1m, totalRevenue) * 0.7m)
                alerts.Add("The platform's revenue is highly concentrated among the leading companies in the base.");
            if (totalCompanies > 0 && activeCompanies / (decimal)totalCompanies < 0.7m)
                alerts.Add("The share of active companies over the total base is below 70%.");
            return alerts;
        }


        private List<(DateTime start, DateTime end)> ExpandOccurrences(
            string rrule,
            DateTime startLocal,
            DateTime endLocal,
            DateTime? endLocalSeries,
            int? count)
        {
            var rule = ParseRRule(rrule);
            var list = new List<(DateTime, DateTime)>();
            var duration = endLocal - startLocal;
            var occurrences = 0;
            var cursor = startLocal;
            var timeOfDay = startLocal.TimeOfDay;
            var limit = endLocalSeries ?? startLocal.AddYears(2);

            if (rule.Freq == "DAILY")
            {
                while (cursor <= limit && (count == null || occurrences < count.Value))
                {
                    list.Add((cursor, cursor + duration));
                    occurrences += 1;
                    cursor = cursor.AddDays(rule.Interval);
                }
            }
            else if (rule.Freq == "WEEKLY")
            {
                var days = rule.ByDay.Count > 0
                    ? rule.ByDay.Select(d => d.ToUpperInvariant()).Distinct().OrderBy(DaySortKey).ToList()
                    : new List<string> { DayToByDay(cursor.DayOfWeek) };

                var weekStart = cursor.Date;
                while (weekStart <= limit && (count == null || occurrences < count.Value))
                {
                    foreach (var day in days)
                    {
                        var dayDate = NextOnOrAfter(weekStart, day);
                        if (dayDate < startLocal.Date) continue;
                        if (dayDate > limit) break;

                        var startCandidate = dayDate.Date + timeOfDay;
                        if (startCandidate < startLocal) continue;
                        if (startCandidate > limit) continue;

                        list.Add((startCandidate, startCandidate + duration));
                        occurrences += 1;
                        if (count != null && occurrences >= count.Value) break;
                    }

                    weekStart = weekStart.AddDays(7 * rule.Interval);
                }
            }
            else if (rule.Freq == "MONTHLY")
            {
                var targetDays = rule.ByMonthDay.Count > 0 ? rule.ByMonthDay : new List<int> { startLocal.Day };
                var monthCursor = new DateTime(startLocal.Year, startLocal.Month, 1);

                while (monthCursor <= limit && (count == null || occurrences < count.Value))
                {
                    foreach (var targetDay in targetDays.OrderBy(x => x))
                    {
                        var daysInMonth = DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
                        if (targetDay < 1 || targetDay > daysInMonth) continue;

                        var startCandidate = new DateTime(monthCursor.Year, monthCursor.Month, targetDay).Date + timeOfDay;
                        if (startCandidate < startLocal) continue;
                        if (startCandidate > limit) continue;

                        list.Add((startCandidate, startCandidate + duration));
                        occurrences += 1;
                        if (count != null && occurrences >= count.Value) break;
                    }

                    monthCursor = monthCursor.AddMonths(rule.Interval);
                }
            }

            return list;
        }

        private sealed class ParsedRRule
        {
            public string Freq { get; set; } = "DAILY";
            public int Interval { get; set; } = 1;
            public List<string> ByDay { get; set; } = new();
            public List<int> ByMonthDay { get; set; } = new();
        }

        private static ParsedRRule ParseRRule(string rrule)
        {
            var rule = new ParsedRRule();
            var parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;

                var key = kv[0].ToUpperInvariant();
                var value = kv[1].Trim();
                if (key == "FREQ") rule.Freq = value.ToUpperInvariant();
                else if (key == "INTERVAL" && int.TryParse(value, out var interval)) rule.Interval = Math.Max(1, interval);
                else if (key == "BYDAY")
                    rule.ByDay = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => x.ToUpperInvariant())
                        .ToList();
                else if (key == "BYMONTHDAY")
                    rule.ByMonthDay = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => int.TryParse(x, out var day) ? day : 0)
                        .Where(x => x != 0)
                        .ToList();
            }
            return rule;
        }

        private static int DaySortKey(string byDay) => byDay switch
        {
            "MO" => 1,
            "TU" => 2,
            "WE" => 3,
            "TH" => 4,
            "FR" => 5,
            "SA" => 6,
            "SU" => 7,
            _ => 99,
        };

        private static string DayToByDay(DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Monday => "MO",
            DayOfWeek.Tuesday => "TU",
            DayOfWeek.Wednesday => "WE",
            DayOfWeek.Thursday => "TH",
            DayOfWeek.Friday => "FR",
            DayOfWeek.Saturday => "SA",
            DayOfWeek.Sunday => "SU",
            _ => "MO",
        };

        private static DateTime NextOnOrAfter(DateTime weekStart, string byDay)
        {
            var target = byDay switch
            {
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday,
                _ => weekStart.DayOfWeek,
            };

            var date = weekStart.Date;
            while (date.DayOfWeek != target)
                date = date.AddDays(1);
            return date;
        }

        private static ReportKpiCardDto MakeCard(string key, string label, decimal value, string displayValue, decimal? changePct, string? description)
        {
            var trend = "neutral";
            if (changePct.HasValue)
                trend = changePct.Value > 0 ? "up" : changePct.Value < 0 ? "down" : "neutral";

            return new ReportKpiCardDto
            {
                Key = key,
                Label = label,
                Value = value,
                DisplayValue = displayValue,
                ChangePercentage = changePct,
                Trend = trend,
                Description = description,
            };
        }

        private static decimal? ChangePct(decimal current, decimal previous)
        {
            if (previous == 0 && current == 0) return 0m;
            if (previous == 0) return 100m;
            return Math.Round(((current - previous) / previous) * 100m, 2);
        }

        private static string FormatCurrency(decimal value) => value.ToString("C", new CultureInfo("en-US"));
        private static string FormatPct(decimal value) => $"{value:0.0}%";
        private static string FormatSignedPct(decimal? value) => value.HasValue ? $"{(value.Value >= 0 ? "+" : string.Empty)}{value.Value:0.0}%" : "0.0%";
        private static string FormatDate(DateTime value) => value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        private static string FormatDateTime(DateTime value) => value.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
        private static string FormatInt(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
        private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize <= 0 ? 20 : pageSize, 5, 100);
        private static string Escape(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    }
}
