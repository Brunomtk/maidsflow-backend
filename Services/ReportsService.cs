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
                ?? throw new InvalidOperationException("Company não encontrada.");

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
                    Name = g.Key.HasValue && serviceTypes.TryGetValue(g.Key.Value, out var serviceName) ? serviceName : "Sem serviço",
                    PrimaryValue = g.Count(),
                    PrimaryLabel = "appointments",
                    SecondaryValue = g.Count(x => x.Status == AppointmentStatus.Completed),
                    SecondaryLabel = "concluídos",
                    Badge = g.Count(x => x.IsRecurring) > 0 ? $"{g.Count(x => x.IsRecurring)} recorrentes" : null,
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
                    MakeCard("revenue_paid", "Revenue collected", completedRevenue, FormatCurrency(completedRevenue), ChangePct(completedRevenue, previousCompletedRevenue), "Somente pagamentos marcados como pagos no período."),
                    MakeCard("customers_active", "Customers ativos", activeCustomerIds.Count, FormatInt(activeCustomerIds.Count), null, "Customers com ao menos um atendimento no período."),
                },
                Financial = new CompanyReportFinancialDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Financial",
                        Summary = $"The company generated {FormatCurrency(completedRevenue)} in collected revenue during the period, with an average ticket of {FormatCurrency(averageTicket)} and a collection efficiency of {FormatPct(collectionRate)} over the billed amount.",
                        Highlights = new List<string>
                        {
                            $"Revenue collected variou {FormatSignedPct(ChangePct(completedRevenue, previousCompletedRevenue))} compared with the previous period.",
                            $"Each active customer generated an average of {FormatCurrency(revenuePerActiveCustomer)} in revenue during the analyzed period.",
                            $"There is {FormatCurrency(receivableAmount)} still open, of which {FormatCurrency(overdueAmount)} is already overdue."
                        },
                        Alerts = BuildFinancialAlerts(receivableAmount, overdueAmount, collectionRate, completedRevenue, averageTicket)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("revenue_total", "Revenue collected", completedRevenue, FormatCurrency(completedRevenue), ChangePct(completedRevenue, previousCompletedRevenue), "Payments effectively collected within the selected period."),
                        MakeCard("receivable_amount", "Open balance", receivableAmount, FormatCurrency(receivableAmount), null, "Sum of pending and overdue payments."),
                        MakeCard("average_ticket", "Ticket médio", averageTicket, FormatCurrency(averageTicket), null, "Revenue collected dividida pelo total de appointments."),
                        MakeCard("collection_rate", "Collection efficiency", collectionRate, FormatPct(collectionRate), null, "Percentage of the billed amount in the period already marked as paid."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Revenue per active customer", Value = FormatCurrency(revenuePerActiveCustomer), Description = "Quanto cada cliente ativo gerou em média no período." },
                        new() { Label = "Revenue per day", Value = FormatCurrency(period.TotalDays > 0 ? completedRevenue / period.TotalDays : 0m), Description = "Média diária de receita recebida." },
                        new() { Label = "Open balance vs. billed amount", Value = FormatPct(totalBilledAmount > 0 ? receivableAmount / totalBilledAmount * 100m : 0m), Description = "Peso do saldo em aberto dentro do faturamento do período." },
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
                            new() { Key = "date", Label = "Data" },
                            new() { Key = "reference", Label = "Reference" },
                            new() { Key = "customer", Label = "Customer" },
                            new() { Key = "status", Label = "Status" },
                            new() { Key = "method", Label = "Método" },
                            new() { Key = "amount", Label = "Valor" },
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
                                    ["customer"] = customers.FirstOrDefault(c => c.Id == x.CustomerId)?.Name ?? "Sem cliente",
                                    ["status"] = x.Status.ToString(),
                                    ["method"] = x.Method?.ToString() ?? "Não informado",
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
                        Title = "Operações",
                        Summary = $"A operação registrou {FormatInt(appointmentTotal)} appointments no período, com média de {dailyAverageAppointments.ToString("0.0", CultureInfo.InvariantCulture)} por dia, taxa de conclusão de {FormatPct(completionRate)} e cancelamento de {FormatPct(cancellationRate)}.",
                        Highlights = new List<string>
                        {
                            $"A variação de volume frente ao período anterior foi de {FormatSignedPct(ChangePct(appointmentTotal, previousAppointmentTotal))}.",
                            $"{FormatPct(recurringShare)} da agenda analisada veio de atendimentos recorrentes.",
                            $"Foram concluídos {FormatInt(completedCount)} atendimentos e cancelados {FormatInt(cancelledCount)} no intervalo selecionado."
                        },
                        Alerts = BuildOperationsAlerts(cancellationRate, completionRate, recurringShare, dailyAverageAppointments)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("appointments_total", "Appointments", appointmentTotal, FormatInt(appointmentTotal), ChangePct(appointmentTotal, previousAppointmentTotal), "Volume operacional total."),
                        MakeCard("completed_total", "Concluídos", completedCount, FormatInt(completedCount), ChangePct(completedCount, previousCompletedCount), "Atendimentos finalizados com sucesso."),
                        MakeCard("scheduled_total", "Agendados", scheduledCount, FormatInt(scheduledCount), null, "Appointments ainda programados."),
                        MakeCard("cancellation_rate", "Taxa de cancelamento", cancellationRate, FormatPct(cancellationRate), ChangePct(cancellationRate, previousAppointmentTotal > 0 ? previousCancelledCount / (decimal)previousAppointmentTotal * 100m : 0m), "Peso dos cancelamentos sobre o total do período."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Média diária de appointments", Value = dailyAverageAppointments.ToString("0.0", CultureInfo.InvariantCulture), Description = "Volume médio por dia corrido no período." },
                        new() { Label = "Share de recorrência", Value = FormatPct(recurringShare), Description = "Quanto da agenda veio de serviços recorrentes." },
                        new() { Label = "Appointments por cliente ativo", Value = activeCustomerIds.Count > 0 ? (appointmentTotal / (decimal)activeCustomerIds.Count).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Intensidade média de atendimento por cliente ativo." },
                    },
                    AppointmentsTrend = BuildDateSeries(period.StartDate, period.EndDate, appointments, x => x.Start, _ => 1m),
                    StatusBreakdown = BuildStatusBreakdown(appointmentTotal, scheduledCount, inProgressCount, completedCount, cancelledCount),
                    TopServices = serviceRows.Take(8).ToList(),
                    RecentAppointments = new ReportTableDto
                    {
                        Title = "Appointments recentes",
                        Description = "Base operacional pronta para exportação no PDF, útil para auditoria e leitura detalhada por serviço.",
                        Columns = new List<ReportTableColumnDto>
                        {
                            new() { Key = "start", Label = "Data" },
                            new() { Key = "title", Label = "Appointment" },
                            new() { Key = "customer", Label = "Customer" },
                            new() { Key = "service", Label = "Serviço" },
                            new() { Key = "status", Label = "Status" },
                            new() { Key = "team", Label = "Profissionais" },
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
                                    ["customer"] = customers.FirstOrDefault(c => c.Id == x.CustomerId)?.Name ?? "Sem cliente",
                                    ["service"] = x.ServiceTypeId.HasValue && serviceTypes.TryGetValue(x.ServiceTypeId.Value, out var serviceName) ? serviceName : (x.Category ?? "Sem serviço"),
                                    ["status"] = x.Status.ToString(),
                                    ["team"] = string.Join(", ", professionals.Where(p => x.ProfessionalIds.Contains(p.Id)).Select(p => p.Name).DefaultIfEmpty("Não vinculado")),
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
                        Title = "Equipe",
                        Summary = $"A equipe teve {FormatInt(professionals.Count)} professionals cadastrados, sendo {FormatInt(professionals.Count(x => x.Status == StatusEnum.Active))} ativos. O rating médio consolidado ficou em {averageRating.ToString("0.0", CultureInfo.InvariantCulture)}.",
                        Highlights = new List<string>
                        {
                            $"{FormatInt(teamRows.Count)} professionals participaram efetivamente da agenda filtrada.",
                            $"A média de conclusões por professional engajado foi de {(teamRows.Count > 0 ? teamRows.Average(x => x.PrimaryValue).ToString("0.0", CultureInfo.InvariantCulture) : "0.0")}.",
                            $"O volume de receita estimada por alocação ajuda a identificar concentração operacional na equipe."
                        },
                        Alerts = BuildTeamAlerts(teamRows, professionals.Count, averageRating)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("professionals_active", "Professionals ativos", professionals.Count(x => x.Status == StatusEnum.Active), FormatInt(professionals.Count(x => x.Status == StatusEnum.Active)), null, "Profissionais ativos no cadastro."),
                        MakeCard("professionals_utilized", "Professionals com agenda", teamRows.Count, FormatInt(teamRows.Count), null, "Profissionais que apareceram em ao menos um appointment do período."),
                        MakeCard("average_rating", "Rating médio", averageRating, averageRating.ToString("0.0", CultureInfo.InvariantCulture), null, "Média das reviews recebidas no período."),
                        MakeCard("completed_per_professional", "Conclusões / professional", teamRows.Count > 0 ? teamRows.Average(x => x.PrimaryValue) : 0m, teamRows.Count > 0 ? teamRows.Average(x => x.PrimaryValue).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", null, "Produtividade média dos professionals engajados."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Utilização da equipe", Value = FormatPct(professionals.Count > 0 ? teamRows.Count / (decimal)professionals.Count * 100m : 0m), Description = "Percentual de professionals cadastrados que tiveram agenda no período." },
                        new() { Label = "Receita estimada por professional", Value = FormatCurrency(teamRows.Count > 0 ? teamRows.Average(x => x.SecondaryValue ?? 0m) : 0m), Description = "Média estimada com base no vínculo entre appointments e clientes pagantes." },
                        new() { Label = "Concentração do líder", Value = FormatPct(teamRows.Any() ? teamRows.First().PrimaryValue / Math.Max(1m, teamRows.Sum(x => x.PrimaryValue)) * 100m : 0m), Description = "Peso do profissional mais produtivo no total de conclusões." },
                    },
                    Leaderboard = teamRows.Take(10).ToList(),
                },
                Customers = new CompanyReportCustomersDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Customers",
                        Summary = $"A base analisada teve {FormatInt(newCustomers)} novos clientes no período, {FormatInt(activeCustomerIds.Count)} clientes ativos e {FormatInt(recurringCustomerCount)} clientes recorrentes, indicando o nível de retenção e dependência da carteira atual.",
                        Highlights = new List<string>
                        {
                            $"A recorrência entre clientes ativos ficou em {FormatPct(activeCustomerIds.Count > 0 ? recurringCustomerCount / (decimal)activeCustomerIds.Count * 100m : 0m)}.",
                            $"Os 5 maiores clientes concentram {FormatPct(customerRows.Take(5).Sum(x => x.PrimaryValue) / Math.Max(1m, completedRevenue) * 100m)} da receita recebida.",
                            $"A empresa atendeu {FormatInt(activeCustomerIds.Count)} clientes diferentes no intervalo filtrado."
                        },
                        Alerts = BuildCustomerAlerts(newCustomers, activeCustomerIds.Count, recurringCustomerCount, completedRevenue, customerRows)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("new_customers", "Customers novos", newCustomers, FormatInt(newCustomers), null, "Customers cadastrados dentro do período selecionado."),
                        MakeCard("active_customers", "Customers ativos", activeCustomerIds.Count, FormatInt(activeCustomerIds.Count), null, "Customers com ao menos um atendimento no período."),
                        MakeCard("recurring_customers", "Customers recorrentes", recurringCustomerCount, FormatInt(recurringCustomerCount), null, "Customers com mais de um appointment no período."),
                        MakeCard("avg_revenue_per_customer", "Revenue per active customer", revenuePerActiveCustomer, FormatCurrency(revenuePerActiveCustomer), null, "Revenue collected dividida pelos clientes ativos."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Novos sobre ativos", Value = FormatPct(activeCustomerIds.Count > 0 ? newCustomers / (decimal)activeCustomerIds.Count * 100m : 0m), Description = "Peso da aquisição recente na carteira ativa." },
                        new() { Label = "Recorrência da carteira", Value = FormatPct(activeCustomerIds.Count > 0 ? recurringCustomerCount / (decimal)activeCustomerIds.Count * 100m : 0m), Description = "Participação dos clientes com repetição de serviço." },
                        new() { Label = "Receita média dos top 5", Value = FormatCurrency(customerRows.Take(5).Any() ? customerRows.Take(5).Average(x => x.PrimaryValue) : 0m), Description = "Ticket médio de valor dos maiores clientes do período." },
                    },
                    TopCustomers = customerRows.Take(10).ToList(),
                    CustomerActivityTable = new ReportTableDto
                    {
                        Title = "Atividade de clientes",
                        Description = "Tabela detalhada para compor o PDF com frequência de atendimento e participação de receita por cliente.",
                        Columns = new List<ReportTableColumnDto>
                        {
                            new() { Key = "customer", Label = "Customer" },
                            new() { Key = "appointments", Label = "Appointments" },
                            new() { Key = "completed", Label = "Concluídos" },
                            new() { Key = "revenue", Label = "Receita" },
                            new() { Key = "badge", Label = "Perfil" },
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
                                    ["badge"] = x.Badge ?? "Pontual",
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
                throw new InvalidOperationException("Use o endpoint company para relatórios da empresa logada.");

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
                    PrimaryLabel = "receita",
                    SecondaryValue = companyAppointments.Count,
                    SecondaryLabel = "appointments",
                    Badge = company.Status == StatusEnum.Active ? "Ativa" : company.Status.ToString(),
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
                    MakeCard("companies_total", "Companies", companies.Count, FormatInt(companies.Count), null, "Base total de empresas cadastradas."),
                    MakeCard("companies_active", "Companies ativas", activeCompanies, FormatInt(activeCompanies), null, "Empresas com status ativo."),
                    MakeCard("appointments_total", "Appointments", appointmentTotal, FormatInt(appointmentTotal), ChangePct(appointmentTotal, previousAppointmentTotal), "Volume operacional total do período."),
                    MakeCard("revenue_paid", "Revenue collected", totalRevenue, FormatCurrency(totalRevenue), ChangePct(totalRevenue, previousRevenue), "Receita efetivamente paga no período."),
                },
                Billing = new AdminReportBillingDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Billing",
                        Summary = $"A plataforma registrou {FormatCurrency(totalRevenue)} em receita recebida, com eficiência de cobrança de {FormatPct(collectionRate)} e {FormatCurrency(overdueAmount)} em valores overdue no período filtrado.",
                        Highlights = new List<string>
                        {
                            $"There is {FormatInt(activeSubscriptions)} subscriptions ativas na base.",
                            $"{FormatInt(companiesWithAppointments)} companies tiveram uso operacional dentro do período.",
                            $"A variação de receita em relação ao período anterior foi de {FormatSignedPct(ChangePct(totalRevenue, previousRevenue))}."
                        },
                        Alerts = BuildAdminBillingAlerts(overdueAmount, collectionRate, activeSubscriptions, activeCompanies)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("subscriptions_active", "Active subscriptions", activeSubscriptions, FormatInt(activeSubscriptions), null, "Assinaturas com status ativo."),
                        MakeCard("companies_with_usage", "Companies with usage", companiesWithAppointments, FormatInt(companiesWithAppointments), null, "Empresas com ao menos um appointment no período."),
                        MakeCard("overdue_amount", "Overdue amount", overdueAmount, FormatCurrency(overdueAmount), null, "Overdue charges during the period."),
                        MakeCard("collection_rate", "Collection efficiency", collectionRate, FormatPct(collectionRate), null, "Receita paga sobre total faturado no período."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Receita por company ativa", Value = FormatCurrency(activeCompanies > 0 ? totalRevenue / activeCompanies : 0m), Description = "Monetização média por empresa ativa." },
                        new() { Label = "Uso operacional da base", Value = FormatPct(companies.Count > 0 ? companiesWithAppointments / (decimal)companies.Count * 100m : 0m), Description = "Percentual da base com movimentação operacional no período." },
                        new() { Label = "Overdue sobre faturado", Value = FormatPct(totalBilled > 0 ? overdueAmount / totalBilled * 100m : 0m), Description = "Peso do saldo vencido dentro do faturamento do período." },
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
                            SecondaryLabel = "cobranças",
                            Badge = "Atenção",
                        })
                        .OrderByDescending(x => x.PrimaryValue)
                        .Take(10)
                        .ToList(),
                },
                Operations = new AdminReportOperationsDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Operações",
                        Summary = $"A operação consolidada da plataforma registrou {FormatInt(appointmentTotal)} appointments, com taxa de conclusão de {FormatPct(completionRate)} e taxa de cancelamento de {FormatPct(appointmentTotal > 0 ? cancelledTotal / (decimal)appointmentTotal * 100m : 0m)}.",
                        Highlights = new List<string>
                        {
                            $"A base total possui {FormatInt(customers.Count)} clientes e {FormatInt(professionals.Count)} professionals cadastrados.",
                            $"A variação de volume operacional contra o período anterior foi de {FormatSignedPct(ChangePct(appointmentTotal, previousAppointmentTotal))}.",
                            $"O monitoramento por status mostra equilíbrio entre agendados, em andamento e concluídos, útil para leitura executiva no PDF."
                        },
                        Alerts = BuildAdminOperationsAlerts(completionRate, appointmentTotal, cancelledTotal, companiesWithAppointments, companies.Count)
                    },
                    Cards = new List<ReportKpiCardDto>
                    {
                        MakeCard("completion_rate", "Completion rate", completionRate, FormatPct(completionRate), null, "Concluídos sobre total de appointments."),
                        MakeCard("completed_total", "Concluídos", completedTotal, FormatInt(completedTotal), null, "Appointments finalizados com sucesso."),
                        MakeCard("customers_total", "Customers", customers.Count, FormatInt(customers.Count), null, "Customers totais na base."),
                        MakeCard("professionals_total", "Professionals", professionals.Count, FormatInt(professionals.Count), null, "Profissionais totais na base."),
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Appointments por company com uso", Value = companiesWithAppointments > 0 ? (appointmentTotal / (decimal)companiesWithAppointments).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Intensidade média de uso por empresa ativa operacionalmente." },
                        new() { Label = "Customers por company", Value = companies.Count > 0 ? (customers.Count / (decimal)companies.Count).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Escala média de carteira por empresa da base." },
                        new() { Label = "Professionals por company", Value = companies.Count > 0 ? (professionals.Count / (decimal)companies.Count).ToString("0.0", CultureInfo.InvariantCulture) : "0.0", Description = "Capacidade média de equipe por empresa." },
                    },
                    AppointmentsTrend = BuildDateSeries(period.StartDate, period.EndDate, appointments, x => x.Start, _ => 1m),
                    StatusBreakdown = BuildStatusBreakdown(appointmentTotal, scheduledTotal, inProgressTotal, completedTotal, cancelledTotal),
                },
                Companies = new AdminReportCompaniesDto
                {
                    Narrative = new ReportSectionNarrativeDto
                    {
                        Title = "Companies",
                        Summary = $"O ranking consolidado evidencia quais companies puxam receita e volume operacional, ajudando o front a gerar um PDF executivo com comparação entre tenants, concentração de resultado e exposição a risco financeiro.",
                        Highlights = new List<string>
                        {
                            $"As 5 principais companies concentram {FormatPct(companyRanking.Take(5).Sum(x => x.PrimaryValue) / Math.Max(1m, totalRevenue) * 100m)} da receita recebida.",
                            $"{FormatInt(activeCompanies)} companies estão ativas dentro de uma base total de {FormatInt(companies.Count)} empresas.",
                            $"O ranking combina receita e volume operacional para evitar leitura cega baseada em um único eixo."
                        },
                        Alerts = BuildAdminCompanyAlerts(companyRanking, totalRevenue, activeCompanies, companies.Count)
                    },
                    Benchmarks = new List<ReportBenchmarkDto>
                    {
                        new() { Label = "Receita média top 5", Value = FormatCurrency(companyRanking.Take(5).Any() ? companyRanking.Take(5).Average(x => x.PrimaryValue) : 0m), Description = "Média de receita entre as líderes da base." },
                        new() { Label = "Receita média geral", Value = FormatCurrency(companies.Count > 0 ? totalRevenue / companies.Count : 0m), Description = "Distribuição média de receita por company cadastrada." },
                        new() { Label = "Participação das ativas", Value = FormatPct(companies.Count > 0 ? activeCompanies / (decimal)companies.Count * 100m : 0m), Description = "Peso das empresas ativas sobre a base total." },
                    },
                    Ranking = companyRanking.Take(10).ToList(),
                    CompaniesTable = new ReportTableDto
                    {
                        Title = "Ranking de companies",
                        Description = "Tabela consolidada para composição do PDF administrativo e comparação entre empresas da plataforma.",
                        Columns = new List<ReportTableColumnDto>
                        {
                            new() { Key = "company", Label = "Company" },
                            new() { Key = "status", Label = "Status" },
                            new() { Key = "revenue", Label = "Receita" },
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
                throw new InvalidOperationException("Use o endpoint admin para relatórios globais.");

            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new InvalidOperationException("Escopo de company não encontrado.");

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
                $"Período: {period.StartDate:dd/MM/yyyy} até {period.EndDate:dd/MM/yyyy}"
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
                DisplayPeriod = $"{period.StartDate:dd/MM/yyyy} - {period.EndDate:dd/MM/yyyy}",
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
                    PrimaryLabel = "concluídos",
                    SecondaryValue = estimatedRevenue,
                    SecondaryLabel = "receita estimada",
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
                        PrimaryLabel = "receita",
                        SecondaryValue = appts.Count,
                        SecondaryLabel = "appointments",
                        Badge = appts.Count > 1 ? "Recorrente" : "Pontual",
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
                strengths.Add($"A receita recebida se manteve em trajetória positiva, com variação de {FormatSignedPct(ChangePct(revenue, previousRevenue))} contra o período anterior.");
            if (completionRate >= 80m)
                strengths.Add($"A taxa de conclusão de {FormatPct(completionRate)} indica execução operacional saudável.");
            if (averageRating >= 4m)
                strengths.Add($"A percepção do cliente foi favorável, com rating médio de {averageRating.ToString("0.0", CultureInfo.InvariantCulture)}.");
            if (recurringCustomers > 0)
                strengths.Add($"A carteira mostra retenção ativa, com {FormatInt(recurringCustomers)} clientes recorrentes no período.");

            if (cancellationRate >= 15m)
                risks.Add($"A taxa de cancelamento em {FormatPct(cancellationRate)} merece investigação de causas operacionais e comerciais.");
            if (overdueAmount > 0)
                risks.Add($"Há {FormatCurrency(overdueAmount)} em valores overdue, o que pressiona o caixa e reduz previsibilidade.");
            if (receivableAmount > revenue && receivableAmount > 0)
                risks.Add("O saldo em aberto já supera a receita recebida no período, sinalizando risco de cobrança.");
            if (activeCustomers == 0)
                risks.Add("Não houve clientes ativos no período filtrado, o que pode indicar filtro excessivo ou baixa operação.");

            recommendedActions.Add("Usar o PDF para comparar receita, cancelamento e retenção ao longo dos próximos períodos e medir tendência, não só fotografia isolada.");
            if (overdueAmount > 0)
                recommendedActions.Add("Priorizar uma régua de cobrança para reduzir o overdue e melhorar a conversão do faturado em caixa real.");
            if (cancellationRate >= 10m)
                recommendedActions.Add("Analisar motivos de cancelamento por serviço, cliente e profissional para atacar o gargalo onde ele realmente mora.");
            if (newCustomers > 0 && recurringCustomers < newCustomers)
                recommendedActions.Add("Criar ação de retenção para converter aquisição recente em recorrência real.");

            var healthStatus = "neutral";
            if (completionRate >= 80m && cancellationRate < 10m && overdueAmount <= 0)
                healthStatus = "good";
            else if (cancellationRate >= 15m || overdueAmount > 0)
                healthStatus = "attention";

            return new ReportExecutiveSummaryDto
            {
                Headline = $"Resumo executivo — {companyName}",
                HealthStatus = healthStatus,
                Narrative = $"No período analisado, a empresa movimentou {FormatInt(appointments)} appointments e {FormatCurrency(revenue)} em receita recebida. A operação fechou com taxa de conclusão de {FormatPct(completionRate)}, cancelamento de {FormatPct(cancellationRate)} e share de recorrência de {FormatPct(recurringShare)}.",
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

            strengths.Add($"A plataforma tem {FormatInt(activeCompanies)} companies ativas dentro de uma base de {FormatInt(totalCompanies)} empresas.");
            strengths.Add($"Foram registradas {FormatInt(activeSubscriptions)} subscriptions ativas, sustentando a leitura de monetização da base.");
            if (ChangePct(revenue, previousRevenue) >= 0)
                strengths.Add($"A receita recebida variou {FormatSignedPct(ChangePct(revenue, previousRevenue))} frente ao período anterior.");

            if (overdueAmount > 0)
                risks.Add($"A base concentra {FormatCurrency(overdueAmount)} em overdue, o que exige acompanhamento de cobrança.");
            if (collectionRate < 70m)
                risks.Add($"A eficiência de cobrança está em {FormatPct(collectionRate)}, abaixo do ideal para previsibilidade de caixa.");
            if (ChangePct(appointments, previousAppointments) < 0)
                risks.Add("O volume operacional caiu frente ao período anterior e pode indicar retração de uso em parte da base.");

            recommendedActions.Add("Usar o PDF administrativo para destacar empresas líderes, risco de inadimplência e densidade de uso da plataforma.");
            recommendedActions.Add("Cruzar companies com maior overdue contra companies com menor uso para identificar risco de churn e cobrança.");
            recommendedActions.Add("Monitorar evolução de receita por company ativa para distinguir crescimento saudável de concentração excessiva.");

            var healthStatus = overdueAmount > 0 || collectionRate < 70m ? "attention" : "good";

            return new ReportExecutiveSummaryDto
            {
                Headline = "Resumo executivo — Plataforma",
                HealthStatus = healthStatus,
                Narrative = $"No período analisado, a plataforma movimentou {FormatInt(appointments)} appointments e {FormatCurrency(revenue)} em receita recebida, com eficiência de cobrança de {FormatPct(collectionRate)}.",
                Strengths = strengths.Take(4).ToList(),
                Risks = risks.Take(4).ToList(),
                RecommendedActions = recommendedActions.Take(4).ToList(),
            };
        }

        private static List<string> BuildFinancialAlerts(decimal receivableAmount, decimal overdueAmount, decimal collectionRate, decimal revenue, decimal averageTicket)
        {
            var alerts = new List<string>();
            if (overdueAmount > 0)
                alerts.Add($"Saldo overdue identificado: {FormatCurrency(overdueAmount)}.");
            if (collectionRate < 70m)
                alerts.Add($"Collection efficiency abaixo do ideal: {FormatPct(collectionRate)}.");
            if (receivableAmount > revenue && receivableAmount > 0)
                alerts.Add("O valor em aberto já supera a receita recebida no período.");
            if (averageTicket <= 0)
                alerts.Add("Não há ticket médio calculável com os dados filtrados.");
            return alerts;
        }

        private static List<string> BuildOperationsAlerts(decimal cancellationRate, decimal completionRate, decimal recurringShare, decimal dailyAverageAppointments)
        {
            var alerts = new List<string>();
            if (cancellationRate >= 15m)
                alerts.Add("Cancelamento elevado para o período analisado.");
            if (completionRate < 70m)
                alerts.Add("Completion rate abaixo de 70%, indicando espaço para ajuste operacional.");
            if (recurringShare < 20m)
                alerts.Add("Baixo share de recorrência; a agenda depende mais de demanda pontual.");
            if (dailyAverageAppointments < 1m)
                alerts.Add("Baixa densidade operacional por dia dentro do período filtrado.");
            return alerts;
        }

        private static List<string> BuildTeamAlerts(List<ReportLeaderboardItemDto> teamRows, int totalProfessionals, decimal averageRating)
        {
            var alerts = new List<string>();
            if (teamRows.Count < totalProfessionals && totalProfessionals > 0)
                alerts.Add("Parte da equipe cadastrada não apareceu na agenda do período, o que pode sinalizar ociosidade ou filtro muito restritivo.");
            if (teamRows.Any() && teamRows.First().PrimaryValue > Math.Max(1m, teamRows.Sum(x => x.PrimaryValue)) * 0.4m)
                alerts.Add("A produtividade está concentrada em poucos professionals, o que aumenta dependência operacional.");
            if (averageRating > 0 && averageRating < 4m)
                alerts.Add("Rating médio abaixo de 4,0; vale investigar feedbacks e experiência do cliente.");
            return alerts;
        }

        private static List<string> BuildCustomerAlerts(int newCustomers, int activeCustomers, int recurringCustomers, decimal revenue, List<ReportLeaderboardItemDto> customerRows)
        {
            var alerts = new List<string>();
            if (activeCustomers > 0 && recurringCustomers < activeCustomers * 0.3m)
                alerts.Add("Baixa recorrência relativa na carteira ativa.");
            if (customerRows.Any() && customerRows.Take(3).Sum(x => x.PrimaryValue) > Math.Max(1m, revenue) * 0.6m)
                alerts.Add("Receita concentrada em poucos clientes; atenção ao risco de dependência.");
            if (newCustomers == 0)
                alerts.Add("Nenhum novo cliente entrou na base dentro do período filtrado.");
            return alerts;
        }

        private static List<string> BuildAdminBillingAlerts(decimal overdueAmount, decimal collectionRate, int activeSubscriptions, int activeCompanies)
        {
            var alerts = new List<string>();
            if (overdueAmount > 0)
                alerts.Add("There is cobranças vencidas na base e elas devem entrar no radar do time financeiro.");
            if (collectionRate < 70m)
                alerts.Add("Collection efficiency abaixo do ideal para uma base previsível.");
            if (activeSubscriptions < activeCompanies)
                alerts.Add("Nem toda company ativa possui subscription ativa; revisar aderência comercial e status contratual.");
            return alerts;
        }

        private static List<string> BuildAdminOperationsAlerts(decimal completionRate, int appointmentTotal, int cancelledTotal, int companiesWithAppointments, int totalCompanies)
        {
            var alerts = new List<string>();
            if (completionRate < 75m)
                alerts.Add("A taxa global de conclusão está abaixo do desejado para a plataforma.");
            if (appointmentTotal > 0 && cancelledTotal / (decimal)appointmentTotal >= 0.15m)
                alerts.Add("O cancelamento consolidado da plataforma está elevado.");
            if (totalCompanies > 0 && companiesWithAppointments / (decimal)totalCompanies < 0.5m)
                alerts.Add("Menos da metade da base teve uso operacional no período.");
            return alerts;
        }

        private static List<string> BuildAdminCompanyAlerts(List<ReportLeaderboardItemDto> companyRanking, decimal totalRevenue, int activeCompanies, int totalCompanies)
        {
            var alerts = new List<string>();
            if (companyRanking.Any() && companyRanking.Take(3).Sum(x => x.PrimaryValue) > Math.Max(1m, totalRevenue) * 0.7m)
                alerts.Add("A receita da plataforma está bastante concentrada nas líderes da base.");
            if (totalCompanies > 0 && activeCompanies / (decimal)totalCompanies < 0.7m)
                alerts.Add("A participação de companies ativas sobre a base total está abaixo de 70%.");
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

        private static string FormatCurrency(decimal value) => value.ToString("C", new CultureInfo("pt-BR"));
        private static string FormatPct(decimal value) => $"{value:0.0}%";
        private static string FormatSignedPct(decimal? value) => value.HasValue ? $"{(value.Value >= 0 ? "+" : string.Empty)}{value.Value:0.0}%" : "0,0%";
        private static string FormatDate(DateTime value) => value.ToString("dd/MM/yyyy");
        private static string FormatDateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm");
        private static string FormatInt(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
        private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize <= 0 ? 20 : pageSize, 5, 100);
        private static string Escape(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    }
}
