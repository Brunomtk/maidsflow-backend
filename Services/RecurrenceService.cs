using System;
using System.Threading.Tasks;
using Core.DTO.Recurrences;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public interface IRecurrenceService
    {
        Task<PagedResult<Recurrence>> GetPagedAsync(RecurrenceFiltersDTO filters);
        Task<Recurrence?> GetByIdAsync(int id);
        Task<Recurrence> CreateAsync(CreateRecurrenceDTO dto);
        Task<Recurrence?> UpdateAsync(int id, UpdateRecurrenceDTO dto);
        Task<bool> DeleteAsync(int id);
    }

    public class RecurrenceService : IRecurrenceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public RecurrenceService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<Recurrence>> GetPagedAsync(RecurrenceFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                filters.CompanyId = scopedCompanyId.Value;
            }

            return await _unitOfWork.Recurrences.GetPagedRecurrencesAsync(filters);
        }

        public async Task<Recurrence?> GetByIdAsync(int id)
        {
            var rec = await _unitOfWork.Recurrences.GetById(id);
            if (rec == null) return null;

            await _scope.EnsureCompanyAccessAsync(rec.CompanyId);
            return rec;
        }

        public async Task<Recurrence> CreateAsync(CreateRecurrenceDTO dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode criar recorrências.");

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");
                dto.CompanyId = scopedCompanyId.Value;
            }

            if (dto.TeamId.HasValue)
                await _scope.EnsureTeamInCompanyAsync(dto.TeamId.Value);

            // CreateRecurrenceDTO.CustomerId é obrigatório (int)
            await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId);

            var now = DateTime.UtcNow;
            var recurrence = new Recurrence
            {
                Title = dto.Title,
                CustomerId = dto.CustomerId,
                Address = dto.Address,
                TeamId = dto.TeamId,
                CompanyId = dto.CompanyId,
                Frequency = dto.Frequency,
                Day = dto.Day,
                Time = dto.Time,
                Duration = dto.Duration,
                Status = dto.Status,
                Type = dto.Type,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Notes = dto.Notes,
                CreatedDate = now,
                UpdatedDate = now
            };

            await _unitOfWork.Recurrences.Add(recurrence);
            await _unitOfWork.SaveAsync();
            return recurrence;
        }

        public async Task<Recurrence?> UpdateAsync(int id, UpdateRecurrenceDTO dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode editar recorrências.");

            var recurrence = await _unitOfWork.Recurrences.GetById(id);
            if (recurrence == null) return null;

            await _scope.EnsureCompanyAccessAsync(recurrence.CompanyId);

            if (dto.TeamId.HasValue)
                await _scope.EnsureTeamInCompanyAsync(dto.TeamId.Value);

            recurrence.Title = dto.Title ?? recurrence.Title;
            recurrence.Address = dto.Address ?? recurrence.Address;
            recurrence.TeamId = dto.TeamId ?? recurrence.TeamId;
            recurrence.Frequency = dto.Frequency ?? recurrence.Frequency;
            recurrence.Day = dto.Day ?? recurrence.Day;
            recurrence.Time = dto.Time ?? recurrence.Time;
            recurrence.Duration = dto.Duration ?? recurrence.Duration;
            recurrence.Status = dto.Status ?? recurrence.Status;
            recurrence.Type = dto.Type ?? recurrence.Type;
            recurrence.StartDate = dto.StartDate ?? recurrence.StartDate;
            recurrence.EndDate = dto.EndDate ?? recurrence.EndDate;
            recurrence.Notes = dto.Notes ?? recurrence.Notes;
            recurrence.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Recurrences.Update(recurrence);
            await _unitOfWork.SaveAsync();
            return recurrence;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode remover recorrências.");

            var recurrence = await _unitOfWork.Recurrences.GetById(id);
            if (recurrence == null) return false;

            await _scope.EnsureCompanyAccessAsync(recurrence.CompanyId);

            _unitOfWork.Recurrences.Delete(recurrence);
            await _unitOfWork.SaveAsync();
            return true;
        }
    }
}
