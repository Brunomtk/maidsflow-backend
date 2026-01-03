// Services/PaymentService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.Payments;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;
using Core.Exceptions;
using Infrastructure.ServiceExtension;

namespace Services
{
    public class PaymentService : IPaymentService
    {
        private readonly Infrastructure.Repositories.IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public PaymentService(Infrastructure.Repositories.IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<Payment>> GetPagedAsync(PaymentFiltersDto filters)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para acessar pagamentos.");

            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (companyId.HasValue) filters.CompanyId = companyId.Value;
            }

            return await _unitOfWork.Payments.GetPagedAsync(filters);
        }

        public async Task<Payment?> GetByIdAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para acessar pagamentos.");

            var payment = await _unitOfWork.Payments.GetByIdAsync(id);
            if (payment == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(payment.CompanyId);

            return payment;
        }

        public async Task<List<Payment>> GetByCustomer(int customerId)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para acessar pagamentos.");

            if (!_currentUser.IsAdmin)
            {
                var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
                if (customer == null) return new List<Payment>();
                await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
            }

            return await _unitOfWork.Payments.GetByCustomerIdAsync(customerId);
        }

        public async Task<Payment> CreateAsync(CreatePaymentDto dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para criar pagamentos.");

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (scopedCompanyId.HasValue) dto.CompanyId = scopedCompanyId.Value;
            }

            var entity = new Payment
            {
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
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
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para editar pagamentos.");

            var entity = await _unitOfWork.Payments.GetByIdAsync(id);
            if (entity == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

            // Não permite trocar CompanyId se não for admin
            if (_currentUser.IsAdmin && dto.CompanyId.HasValue) entity.CompanyId = dto.CompanyId.Value;
            if (dto.CustomerId.HasValue) entity.CustomerId = dto.CustomerId.Value;
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
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para excluir pagamentos.");

            var entity = await _unitOfWork.Payments.GetByIdAsync(id);
            if (entity == null) return false;
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);
            _unitOfWork.Payments.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<Payment> ProcessStatusAsync(int id, ProcessPaymentStatusDto dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para processar pagamentos.");

            var entity = await _unitOfWork.Payments.GetByIdAsync(id);
            if (entity == null) throw new InvalidOperationException("Payment not found");

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

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
        Task<List<Payment>> GetByCustomer(int customerId);
        Task<Payment> CreateAsync(CreatePaymentDto dto);
        Task<Payment?> UpdateAsync(int id, UpdatePaymentDto dto);
        Task<bool> DeleteAsync(int id);
        Task<Payment> ProcessStatusAsync(int id, ProcessPaymentStatusDto dto);
    }
}
