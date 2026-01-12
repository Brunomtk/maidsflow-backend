using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Payroll;
using Core.Enums.Payroll;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;

namespace Services
{
    public interface IPayrollRunService
    {
        Task<List<PayrollRunDTO>> ListByCompanyAsync(int companyId);
        Task<PayrollRunDetailsDTO> GetDetailsAsync(int payrollRunId);
        Task<PayrollRunDetailsDTO> CreateRunAsync(int companyId, CreatePayrollRunRequestDTO dto);
        Task<PayrollRunDTO> CloseAsync(int payrollRunId);
        Task<PayrollRunDTO> MarkPaidAsync(int payrollRunId);
    }

    public class PayrollRunService : IPayrollRunService
    {
        private readonly IUnitOfWork _uow;
        private readonly IPayrollPreviewService _preview;
        private readonly IScopeGuard _scope;

        public PayrollRunService(IUnitOfWork uow, IPayrollPreviewService preview, IScopeGuard scope)
        {
            _uow = uow;
            _preview = preview;
            _scope = scope;
        }

        public async Task<List<PayrollRunDTO>> ListByCompanyAsync(int companyId)
        {
            await _scope.EnsureCompanyAccessAsync(companyId);

            var runs = await _uow.PayrollRuns.GetByCompanyAsync(companyId);

            // N+1 ok por enquanto; se precisar otimizamos depois.
            var result = new List<PayrollRunDTO>();
            foreach (var r in runs)
            {
                var items = await _uow.PayrollItems.GetByRunIdAsync(r.Id);
                result.Add(MapRun(r, items));
            }

            return result
                .OrderByDescending(r => r.PeriodStart)
                .ThenByDescending(r => r.Id)
                .ToList();
        }

        public async Task<PayrollRunDetailsDTO> GetDetailsAsync(int payrollRunId)
        {
            var run = await _uow.PayrollRuns.GetByIdWithItemsAsync(payrollRunId);
            if (run == null)
                throw new NotFoundException("PayrollRun não encontrado.");

            await _scope.EnsureCompanyAccessAsync(run.CompanyId);

            var items = await _uow.PayrollItems.GetByRunIdAsync(payrollRunId);
            var dtoItems = items.Select(MapItem).ToList();

            var summaries = dtoItems
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

            return new PayrollRunDetailsDTO
            {
                Run = MapRun(run, items),
                Items = dtoItems,
                Summaries = summaries
            };
        }

        public async Task<PayrollRunDetailsDTO> CreateRunAsync(int companyId, CreatePayrollRunRequestDTO dto)
        {
            if (dto.PeriodEnd < dto.PeriodStart)
                throw new BadRequestException("PeriodEnd deve ser maior ou igual ao PeriodStart.");

            await _scope.EnsureCompanyAccessAsync(companyId);

            // Preview gera os cálculos (sem persistir)
            var preview = await _preview.PreviewCompanyAsync(companyId, dto.PeriodStart, dto.PeriodEnd);

            if (!dto.AllowMissingRules && preview.TotalMissingRules > 0)
                throw new BadRequestException($"Existem {preview.TotalMissingRules} itens sem regra. Cadastre regras em PayrollRules ou envie AllowMissingRules=true.");

            var run = new PayrollRun
            {
                CompanyId = companyId,
                PeriodStart = dto.PeriodStart,
                PeriodEnd = dto.PeriodEnd,
                Status = PayrollRunStatus.Draft,
                Notes = dto.Notes,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _uow.PayrollRuns.Add(run);
            await _uow.SaveAsync(); // precisa do Id

            foreach (var it in preview.Items)
            {
                var item = new PayrollItem
                {
                    PayrollRunId = run.Id,
                    CompanyId = companyId,
                    ProfessionalId = it.ProfessionalId,
                    AppointmentId = it.AppointmentId,
                    OccurrenceStart = it.OccurrenceStart,
                    OccurrenceEnd = it.OccurrenceEnd,
                    ServiceTypeId = it.ServiceTypeId,
                    Category = it.Category,
                    TeamRole = it.TeamRole,
                    PayrollRuleId = it.PayrollRuleId,
                    PayrollRulePriority = it.PayrollRulePriority,
                    RateType = it.RateType,
                    RateValue = it.RateValue,
                    SourceAmount = it.SourceAmount,
                    CalculatedAmount = it.CalculatedAmount,
                    MissingRule = it.MissingRule,
                    CreatedDate = DateTime.UtcNow
                };

                _uow.PayrollItems.Add(item);
            }

            await _uow.SaveAsync();

            return await GetDetailsAsync(run.Id);
        }

        public async Task<PayrollRunDTO> CloseAsync(int payrollRunId)
        {
            var run = await _uow.PayrollRuns.GetById(payrollRunId);
            if (run == null)
                throw new NotFoundException("PayrollRun não encontrado.");

            await _scope.EnsureCompanyAccessAsync(run.CompanyId);

            if (run.Status != PayrollRunStatus.Draft)
                throw new BadRequestException("Somente PayrollRun em Draft pode ser fechado.");

            var missing = await _uow.PayrollItems.CountMissingRulesAsync(payrollRunId);
            if (missing > 0)
                throw new BadRequestException($"Não é possível fechar: existem {missing} itens sem regra (MissingRule=true).");

            run.Status = PayrollRunStatus.Closed;
            run.ClosedDate = DateTime.UtcNow;
            run.UpdatedDate = DateTime.UtcNow;

            _uow.PayrollRuns.Update(run);
            await _uow.SaveAsync();

            var items = await _uow.PayrollItems.GetByRunIdAsync(payrollRunId);
            return MapRun(run, items);
        }

        public async Task<PayrollRunDTO> MarkPaidAsync(int payrollRunId)
        {
            var run = await _uow.PayrollRuns.GetById(payrollRunId);
            if (run == null)
                throw new NotFoundException("PayrollRun não encontrado.");

            await _scope.EnsureCompanyAccessAsync(run.CompanyId);

            if (run.Status != PayrollRunStatus.Closed)
                throw new BadRequestException("Somente PayrollRun em Closed pode ser marcado como Paid.");

            run.Status = PayrollRunStatus.Paid;
            run.PaidDate = DateTime.UtcNow;
            run.UpdatedDate = DateTime.UtcNow;

            _uow.PayrollRuns.Update(run);
            await _uow.SaveAsync();

            var items = await _uow.PayrollItems.GetByRunIdAsync(payrollRunId);
            return MapRun(run, items);
        }

        private static PayrollRunDTO MapRun(PayrollRun run, List<PayrollItem> items)
        {
            return new PayrollRunDTO
            {
                Id = run.Id,
                CompanyId = run.CompanyId,
                PeriodStart = run.PeriodStart,
                PeriodEnd = run.PeriodEnd,
                Status = run.Status,
                Notes = run.Notes,
                CreatedDate = run.CreatedDate,
                UpdatedDate = run.UpdatedDate,
                ClosedDate = run.ClosedDate,
                PaidDate = run.PaidDate,
                ItemsCount = items.Count,
                MissingRulesCount = items.Count(i => i.MissingRule),
                TotalAmount = items.Sum(i => i.CalculatedAmount)
            };
        }

        private static PayrollItemDTO MapItem(PayrollItem i)
        {
            return new PayrollItemDTO
            {
                Id = i.Id,
                PayrollRunId = i.PayrollRunId,
                CompanyId = i.CompanyId,
                ProfessionalId = i.ProfessionalId,
                ProfessionalName = i.Professional?.Name,
                AppointmentId = i.AppointmentId,
                OccurrenceStart = i.OccurrenceStart,
                OccurrenceEnd = i.OccurrenceEnd,
                AppointmentStart = i.OccurrenceStart,
                AppointmentEnd = i.OccurrenceEnd,
                CustomerId = i.Appointment?.CustomerId,
                CustomerName = i.Appointment?.Customer?.Name,
                ServiceTypeId = i.ServiceTypeId,
                ServiceTypeName = i.ServiceType?.Name,
                Category = i.Category,
                TeamRole = i.TeamRole,
                PayrollRuleId = i.PayrollRuleId,
                PayrollRulePriority = i.PayrollRulePriority,
                RateType = i.RateType,
                RateValue = i.RateValue,
                SourceAmount = i.SourceAmount,
                CalculatedAmount = i.CalculatedAmount,
                MissingRule = i.MissingRule,
                CreatedDate = i.CreatedDate
            };
        }
    }
}
