// Services/CancellationService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.Cancellation;
using Core.Enums;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;
using Core.Exceptions;

namespace Services
{
    public class CancellationService : ICancellationService
    {
        private readonly ICancellationRepository _repo;
        private readonly Infrastructure.Repositories.IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public CancellationService(ICancellationRepository repo, Infrastructure.Repositories.IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _repo = repo;
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<IEnumerable<Cancellation>> GetAllAsync(CancellationFiltersDto filters)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não tem permissão para acessar cancelamentos.");

    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) filters.CompanyId = companyId.Value;
    }

    var list = await _repo.GetAsync(filters);
    return list;
}


        public async Task<Cancellation?> GetByIdAsync(int id)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não tem permissão para acessar cancelamentos.");

    var entity = await _repo.GetByIdAsync(id);
    if (entity == null) return null;

    if (!_currentUser.IsAdmin)
        await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

    return entity;
}


        public async Task<Cancellation> CreateAsync(CreateCancellationDto dto)
{
    if (_currentUser.IsProfessional)
        throw new ForbiddenException("Profissional não tem permissão para criar cancelamentos.");

    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) dto.CompanyId = companyId.Value;
    }

            var now = DateTime.UtcNow;
            var entity = new Cancellation
            {
                AppointmentId = dto.AppointmentId,
                CustomerId = dto.CustomerId,
                CompanyId = dto.CompanyId,
                Reason = dto.Reason,
                CancelledById = dto.CancelledById,
                CancelledByRole = dto.CancelledByRole,
                CancelledAt = now,
                RefundStatus = dto.RefundStatus ?? RefundStatus.Pending,
                Notes = dto.Notes,
                CreatedDate = now,
                UpdatedDate = now
            };

            await _repo.AddAsync(entity);
            await _uow.SaveAsync();
            return entity;
        }

        public async Task<Cancellation?> UpdateAsync(int id, UpdateCancellationDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

            if (!string.IsNullOrEmpty(dto.Reason)) entity.Reason = dto.Reason;
            if (dto.RefundStatus.HasValue) entity.RefundStatus = dto.RefundStatus.Value;
            if (!string.IsNullOrEmpty(dto.Notes)) entity.Notes = dto.Notes;

            entity.UpdatedDate = DateTime.UtcNow;
            _repo.Update(entity);
            await _uow.SaveAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);
            _repo.Delete(entity);
            await _uow.SaveAsync();
            return true;
        }

        public async Task<Cancellation?> ProcessRefundAsync(int id, ProcessRefundDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

            entity.RefundStatus = dto.Status;
            if (!string.IsNullOrEmpty(dto.Notes))
                entity.Notes = dto.Notes;
            entity.UpdatedDate = DateTime.UtcNow;

            _repo.Update(entity);
            await _uow.SaveAsync();
            return entity;
        }
    }
}
public interface ICancellationService
{
    Task<IEnumerable<Cancellation>> GetAllAsync(CancellationFiltersDto filters);
    Task<Cancellation?> GetByIdAsync(int id);
    Task<Cancellation> CreateAsync(CreateCancellationDto dto);
    Task<Cancellation?> UpdateAsync(int id, UpdateCancellationDto dto);
    Task<bool> DeleteAsync(int id);
    Task<Cancellation?> ProcessRefundAsync(int id, ProcessRefundDto dto);
}