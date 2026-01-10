using Core.DTO.PayrollRules;
using Core.Enums.Payroll;
using Core.Enums.Team;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public interface IPayrollRuleService
    {
        Task<List<PayrollRuleDTO>> GetByCompanyAsync(int companyId, bool includeInactive = false);
        Task<PayrollRuleDTO?> GetByIdAsync(int id);
        Task<PayrollRuleDTO> CreateAsync(CreatePayrollRuleDTO dto);
        Task<PayrollRuleDTO> UpdateAsync(int id, UpdatePayrollRuleDTO dto);
        Task<bool> DeleteAsync(int id);
    }

    public class PayrollRuleService : IPayrollRuleService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public PayrollRuleService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<List<PayrollRuleDTO>> GetByCompanyAsync(int companyId, bool includeInactive = false)
        {
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(companyId);

            var list = await _uow.PayrollRules.GetByCompanyAsync(companyId, includeInactive);
            var outList = new List<PayrollRuleDTO>();
            foreach (var r in list)
                outList.Add(ToDto(r));
            return outList;
        }

        public async Task<PayrollRuleDTO?> GetByIdAsync(int id)
        {
            var r = await _uow.PayrollRules.GetByIdWithRelationsAsync(id);
            if (r == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(r.CompanyId);

            return ToDto(r);
        }

        public async Task<PayrollRuleDTO> CreateAsync(CreatePayrollRuleDTO dto)
        {
            // Company scope
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                dto.CompanyId = scopedCompanyId.Value;
            }

            if (!dto.CompanyId.HasValue)
                throw new BadRequestException("CompanyId é obrigatório.");

            ValidateRate(dto.RateType, dto.RateValue);

            var companyId = dto.CompanyId.Value;

            // valida ServiceType
            if (dto.ServiceTypeId.HasValue)
            {
                var st = await _uow.ServiceTypes.GetById(dto.ServiceTypeId.Value);
                if (st == null) throw new BadRequestException("ServiceTypeId inválido.");
                if (st.CompanyId != companyId) throw new ForbiddenException("ServiceType não pertence a esta company.");
            }

            if (await _uow.PayrollRules.ExistsRuleAsync(companyId, dto.ServiceTypeId, dto.TeamRole, dto.Priority))
                throw new BadRequestException("Já existe uma PayrollRule com o mesmo ServiceType/Role/Priority.");

            var entity = new PayrollRule
            {
                CompanyId = companyId,
                ServiceTypeId = dto.ServiceTypeId,
                TeamRole = dto.TeamRole,
                RateType = dto.RateType,
                RateValue = dto.RateValue,
                Priority = dto.Priority,
                IsActive = dto.IsActive ?? true,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            await _uow.PayrollRules.Add(entity);
            await _uow.SaveAsync();

            // carregar ServiceType para DTO
            if (entity.ServiceTypeId.HasValue)
                entity.ServiceType = await _uow.ServiceTypes.GetById(entity.ServiceTypeId.Value);

            return ToDto(entity);
        }

        public async Task<PayrollRuleDTO> UpdateAsync(int id, UpdatePayrollRuleDTO dto)
        {
            var r = await _uow.PayrollRules.GetByIdWithRelationsAsync(id);
            if (r == null) throw new NotFoundException("PayrollRule não encontrada.");

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(r.CompanyId);

            var newServiceTypeId = dto.ServiceTypeId ?? r.ServiceTypeId;
            var newTeamRole = dto.TeamRole ?? r.TeamRole;
            var newPriority = dto.Priority ?? r.Priority;
            var newRateType = dto.RateType ?? r.RateType;
            var newRateValue = dto.RateValue ?? r.RateValue;

            ValidateRate(newRateType, newRateValue);

            if (dto.ServiceTypeId.HasValue)
            {
                if (dto.ServiceTypeId.Value == 0)
                {
                    newServiceTypeId = null;
                }
                else
                {
                    var st = await _uow.ServiceTypes.GetById(dto.ServiceTypeId.Value);
                    if (st == null) throw new BadRequestException("ServiceTypeId inválido.");
                    if (st.CompanyId != r.CompanyId) throw new ForbiddenException("ServiceType não pertence a esta company.");
                }
            }

            if (await _uow.PayrollRules.ExistsRuleAsync(r.CompanyId, newServiceTypeId, newTeamRole, newPriority, ignoreId: id))
                throw new BadRequestException("Já existe uma PayrollRule com o mesmo ServiceType/Role/Priority.");

            r.ServiceTypeId = newServiceTypeId;
            r.TeamRole = newTeamRole;
            r.Priority = newPriority;
            r.RateType = newRateType;
            r.RateValue = newRateValue;

            if (dto.IsActive.HasValue) r.IsActive = dto.IsActive.Value;

            r.UpdatedDate = DateTime.UtcNow;

            _uow.PayrollRules.Update(r);
            await _uow.SaveAsync();

            // recarrega navegação para DTO
            if (r.ServiceTypeId.HasValue)
                r.ServiceType = await _uow.ServiceTypes.GetById(r.ServiceTypeId.Value);
            else
                r.ServiceType = null;

            return ToDto(r);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var r = await _uow.PayrollRules.GetById(id);
            if (r == null) return false;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(r.CompanyId);

            r.IsActive = false;
            r.UpdatedDate = DateTime.UtcNow;
            _uow.PayrollRules.Update(r);
            await _uow.SaveAsync();

            return true;
        }

        private static void ValidateRate(RateType type, decimal value)
        {
            if (value <= 0)
                throw new BadRequestException("RateValue deve ser maior que zero.");

            if (type == RateType.Percent && (value < 0 || value > 100))
                throw new BadRequestException("RateValue (Percent) deve estar entre 0 e 100.");
        }

        private static PayrollRuleDTO ToDto(PayrollRule r) => new PayrollRuleDTO
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            ServiceTypeId = r.ServiceTypeId,
            ServiceTypeName = r.ServiceType?.Name,
            TeamRole = r.TeamRole,
            RateType = r.RateType,
            RateValue = r.RateValue,
            Priority = r.Priority,
            IsActive = r.IsActive
        };
    }
}
