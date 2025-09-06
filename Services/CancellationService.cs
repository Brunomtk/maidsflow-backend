// Services/CancellationService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.Cancellation;
using Core.Enums;
using Core.Models;
using Infrastructure.Repositories;

namespace Services
{
    public class CancellationService : ICancellationService
    {
        private readonly ICancellationRepository _repo;
        private readonly IUnitOfWork _uow;

        public CancellationService(ICancellationRepository repo, IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
        }

        public Task<IEnumerable<Cancellation>> GetAllAsync(CancellationFiltersDto filters)
            => _repo.GetAsync(filters).ContinueWith(t => (IEnumerable<Cancellation>)t.Result);

        public Task<Cancellation?> GetByIdAsync(int id)
            => _repo.GetByIdAsync(id);

        public async Task<Cancellation> CreateAsync(CreateCancellationDto dto)
        {
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
            _repo.Delete(entity);
            await _uow.SaveAsync();
            return true;
        }

        public async Task<Cancellation?> ProcessRefundAsync(int id, ProcessRefundDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return null;

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