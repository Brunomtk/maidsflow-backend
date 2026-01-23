using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Payroll;
using Core.Enums.Payroll;
using Core.Enums.Team;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;

namespace Services
{
    public interface IPayrollPreviewService
    {
        Task<PayrollPreviewResponseDTO> PreviewCompanyAsync(int companyId, DateTime periodStart, DateTime periodEnd);
    }

    /// <summary>
    /// Preview de cálculo de payroll para um período.
    /// Nesta fase (sem PayrollRun/Items persistidos), o cálculo se baseia em:
    /// - Appointments com Status=Completed dentro do range
    /// - Profissionais do Appointment (ProfessionalIds) ou membros do Team (TeamId)
    /// - Regra encontrada em PayrollRules (por ServiceType+Role ou geral+Role)
    /// - Base de cálculo para Percent: Customer.Ticket (se existir)
    /// </summary>
    public class PayrollPreviewService : IPayrollPreviewService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public PayrollPreviewService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PayrollPreviewResponseDTO> PreviewCompanyAsync(int companyId, DateTime periodStart, DateTime periodEnd)
{
    if (periodEnd < periodStart)
        throw new BadRequestException("PeriodEnd deve ser maior ou igual ao PeriodStart.");

    if (!_currentUser.IsAdmin)
        await _scope.EnsureCompanyAccessAsync(companyId);

    // 1) Preferimos AppointmentCompletions (snapshot por ocorrência) para suportar recorrência de forma correta.
    var completions = await _uow.AppointmentCompletions.GetByCompanyAndRangeAsync(companyId, periodStart, periodEnd);

    // 2) Backward-compat: appointments Completed no período que ainda não possuem snapshot.
    var apptsInRange = await _uow.Appointments.GetAppointmentsByDateRangeAsync(periodStart, periodEnd, companyId);
    var completedAppts = apptsInRange
        .Where(a => a.Status == Core.Enums.Appointment.AppointmentStatus.Completed)
        .ToList();

    var completionKey = new HashSet<string>(
        completions.Select(c => $"{c.AppointmentId}|{c.OccurrenceStart:o}")
    );

    var sources = new List<(int AppointmentId, DateTime Start, DateTime End, int? CustomerId, int? TeamId, int? ServiceTypeId, string? Category, List<int> ProfessionalIds, decimal SourceAmount)>();

    // From snapshots
    foreach (var c in completions)
    {
        sources.Add((
            c.AppointmentId,
            c.OccurrenceStart,
            c.OccurrenceEnd,
            c.CustomerIdSnapshot,
            c.TeamIdSnapshot,
            c.ServiceTypeIdSnapshot,
            c.CategorySnapshot,
            c.ProfessionalIdsSnapshot,
            c.SourceAmountSnapshot
        ));
    }

    // From legacy appointments (only when snapshot doesn't exist yet)
    foreach (var a in completedAppts)
    {
        var key = $"{a.Id}|{a.Start:o}";
        if (completionKey.Contains(key)) continue;

        var professionalIds = (a.ProfessionalIds != null && a.ProfessionalIds.Count > 0)
            ? a.ProfessionalIds.Distinct().ToList()
            : new List<int>();

        if (professionalIds.Count == 0 && a.TeamId.HasValue)
        {
            var team = await _uow.Teams.GetByIdWithMembersAsync(a.TeamId.Value);
            if (team != null)
                professionalIds = team.Members.Select(m => m.ProfessionalId).Distinct().ToList();
        }

        sources.Add((
            a.Id,
            a.Start,
            a.End,
            a.CustomerId,
            a.TeamId,
            a.ServiceTypeId,
            a.Category ?? a.Type.ToString(),
            professionalIds,
            a.CustomerAddress?.Ticket ?? a.Customer?.Ticket ?? 0m
        ));
    }

    // Rules da company
    var rules = await _uow.PayrollRules.GetByCompanyAsync(companyId, includeInactive: false);

    // Caches simples
    var teamCache = new Dictionary<int, Team>();
    var professionalNameCache = new Dictionary<int, string?>();
    var customerNameCache = new Dictionary<int, string?>();
    var serviceTypeNameCache = new Dictionary<int, string?>();

    var items = new List<PayrollPreviewItemDTO>();

    foreach (var src in sources)
    {
        var participantIds = src.ProfessionalIds?.Distinct().ToList() ?? new List<int>();
        if (participantIds.Count == 0 && src.TeamId.HasValue)
        {
            var team = await GetTeamAsync(src.TeamId.Value, teamCache);
            participantIds = team.Members.Select(m => m.ProfessionalId).Distinct().ToList();
        }

        if (participantIds.Count == 0)
            continue;

        Team? teamForRole = null;
        if (src.TeamId.HasValue)
            teamForRole = await GetTeamAsync(src.TeamId.Value, teamCache);

        string? customerName = null;
        if (src.CustomerId.HasValue)
            customerName = await GetCustomerNameAsync(src.CustomerId.Value, customerNameCache);

        string? serviceTypeName = null;
        if (src.ServiceTypeId.HasValue)
            serviceTypeName = await GetServiceTypeNameAsync(src.ServiceTypeId.Value, serviceTypeNameCache);

        foreach (var professionalId in participantIds)
        {
            var role = ResolveRole(teamForRole, professionalId);
            var matchedRule = ResolveRule(rules, src.ServiceTypeId, role);

            var sourceAmount = src.SourceAmount;
            var calculated = 0m;
            var missingRule = matchedRule == null;

            if (matchedRule != null)
            {
                calculated = matchedRule.RateType == RateType.Fixed
                    ? matchedRule.RateValue
                    : sourceAmount * (matchedRule.RateValue / 100m);
            }

            var profName = await GetProfessionalNameAsync(professionalId, professionalNameCache);

            items.Add(new PayrollPreviewItemDTO
            {
                AppointmentId = src.AppointmentId,

                OccurrenceStart = src.Start,
                OccurrenceEnd = src.End,

                // Legacy fields
                AppointmentStart = src.Start,
                AppointmentEnd = src.End,

                CompanyId = companyId,
                CustomerId = src.CustomerId,
                CustomerName = customerName,
                TeamId = src.TeamId,
                TeamName = src.TeamId.HasValue ? (await GetTeamAsync(src.TeamId.Value, teamCache)).Name : null,
                ServiceTypeId = src.ServiceTypeId,
                ServiceTypeName = serviceTypeName,
                Category = src.Category,
                ProfessionalId = professionalId,
                ProfessionalName = profName,
                TeamRole = role,
                PayrollRuleId = matchedRule?.Id,
                PayrollRulePriority = matchedRule?.Priority,
                RateType = matchedRule?.RateType,
                RateValue = matchedRule?.RateValue,
                SourceAmount = sourceAmount,
                CalculatedAmount = calculated,
                MissingRule = missingRule
            });
        }
    }

    // Summaries (count distinct occurrences)
    var summaries = items
        .GroupBy(i => i.ProfessionalId)
        .Select(g => new PayrollPreviewProfessionalSummaryDTO
        {
            ProfessionalId = g.Key,
            ProfessionalName = g.FirstOrDefault()?.ProfessionalName,
            AppointmentsCount = g.Select(x => $"{x.AppointmentId}|{x.OccurrenceStart:o}").Distinct().Count(),
            TotalAmount = g.Sum(x => x.CalculatedAmount),
            MissingRulesCount = g.Count(x => x.MissingRule)
        })
        .OrderByDescending(s => s.TotalAmount)
        .ToList();

    return new PayrollPreviewResponseDTO
    {
        CompanyId = companyId,
        PeriodStart = periodStart,
        PeriodEnd = periodEnd,
        Items = items.OrderBy(i => i.OccurrenceStart).ThenBy(i => i.ProfessionalId).ToList(),
        Summaries = summaries,
        TotalAmount = items.Sum(i => i.CalculatedAmount),
        TotalItems = items.Count,
        TotalMissingRules = items.Count(i => i.MissingRule)
    };
}

        private static TeamMemberRole ResolveRole(Team? team, int professionalId)
        {
            if (team == null) return TeamMemberRole.Member;

            var member = team.Members.FirstOrDefault(m => m.ProfessionalId == professionalId);
            if (member == null) return TeamMemberRole.Member;

            // Prefer Role; fallback para IsLeader legado
            if (member.IsLeader) return TeamMemberRole.Leader;
            return member.Role;
        }

        private static PayrollRule? ResolveRule(List<PayrollRule> rules, int? serviceTypeId, TeamMemberRole role)
        {
            // 1) ServiceType específico
            if (serviceTypeId.HasValue)
            {
                var specific = rules
                    .Where(r => r.IsActive && r.TeamRole == role && r.ServiceTypeId == serviceTypeId.Value)
                    .OrderByDescending(r => r.Priority)
                    .FirstOrDefault();
                if (specific != null) return specific;
            }

            // 2) Regra geral
            return rules
                .Where(r => r.IsActive && r.TeamRole == role && r.ServiceTypeId == null)
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault();
        }

        private async Task<Team> GetTeamAsync(int teamId, Dictionary<int, Team> cache)
        {
            if (cache.TryGetValue(teamId, out var cached))
                return cached;

            var team = await _uow.Teams.GetByIdWithMembersAsync(teamId);
            if (team == null)
                throw new NotFoundException("Team não encontrada.");

            cache[teamId] = team;
            return team;
        }

        private async Task<string?> GetProfessionalNameAsync(int professionalId, Dictionary<int, string?> cache)
        {
            if (cache.TryGetValue(professionalId, out var cached))
                return cached;

            var prof = await _uow.Professionals.GetByIdAsync(professionalId);
            var name = prof?.Name;
            cache[professionalId] = name;
            return name;
        }
private async Task<string?> GetCustomerNameAsync(int customerId, Dictionary<int, string?> cache)
{
    if (cache.TryGetValue(customerId, out var cached))
        return cached;

    var customer = await _uow.Customers.GetById(customerId);
    var name = customer?.Name;
    cache[customerId] = name;
    return name;
}

private async Task<string?> GetServiceTypeNameAsync(int serviceTypeId, Dictionary<int, string?> cache)
{
    if (cache.TryGetValue(serviceTypeId, out var cached))
        return cached;

    var st = await _uow.ServiceTypes.GetById(serviceTypeId);
    var name = st?.Name;
    cache[serviceTypeId] = name;
    return name;
}

    }
}
