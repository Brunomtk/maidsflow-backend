// Services/PaymentService.cs
using System;
using System.Threading.Tasks;
using Core.DTO.Payments;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;

namespace Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PaymentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<PagedResult<Payment>> GetPagedAsync(PaymentFiltersDto filters)
            => _unitOfWork.Payments.GetPagedAsync(filters);

        public Task<Payment?> GetByIdAsync(int id)
            => _unitOfWork.Payments.GetByIdAsync(id);

        public async Task<Payment> CreateAsync(CreatePaymentDto dto)
        {
            var entity = new Payment
            {
                CompanyId = dto.CompanyId,
                Amount = dto.Amount,
                DueDate = dto.DueDate,
                PaymentDate = dto.PaymentDate,
                Status = dto.Status,
                Method = dto.Method,
                Reference = dto.Reference,
                PlanId = dto.PlanId,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.Payments.Add(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<Payment?> UpdateAsync(int id, UpdatePaymentDto dto)
        {
            var entity = await _unitOfWork.Payments.GetByIdAsync(id);
            if (entity == null) return null;

            if (dto.CompanyId.HasValue) entity.CompanyId = dto.CompanyId.Value;
            if (dto.Amount.HasValue) entity.Amount = dto.Amount.Value;
            if (dto.DueDate.HasValue) entity.DueDate = dto.DueDate.Value;
            if (dto.PaymentDate.HasValue) entity.PaymentDate = dto.PaymentDate.Value;
            if (dto.Status.HasValue) entity.Status = dto.Status.Value;
            if (dto.Method.HasValue) entity.Method = dto.Method.Value;
            if (!string.IsNullOrEmpty(dto.Reference)) entity.Reference = dto.Reference;
            if (dto.PlanId.HasValue) entity.PlanId = dto.PlanId.Value;

            entity.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.Payments.Update(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _unitOfWork.Payments.GetByIdAsync(id);
            if (entity == null) return false;
            _unitOfWork.Payments.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<Payment> ProcessStatusAsync(int id, ProcessPaymentStatusDto dto)
        {
            var entity = await _unitOfWork.Payments.GetByIdAsync(id);
            if (entity == null) throw new InvalidOperationException("Payment not found");

            entity.Status = dto.Status;
            if (dto.PaymentDate.HasValue)
                entity.PaymentDate = dto.PaymentDate.Value;

            entity.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.Payments.Update(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }
    }

    public interface IPaymentService
    {
        Task<PagedResult<Payment>> GetPagedAsync(PaymentFiltersDto filters);
        Task<Payment?> GetByIdAsync(int id);
        Task<Payment> CreateAsync(CreatePaymentDto dto);
        Task<Payment?> UpdateAsync(int id, UpdatePaymentDto dto);
        Task<bool> DeleteAsync(int id);
        Task<Payment> ProcessStatusAsync(int id, ProcessPaymentStatusDto dto);
    }
}
