// Services/PaymentService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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
            // Company scope: non-admin users are always restricted to their own company.
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (scopedCompanyId.HasValue) dto.CompanyId = scopedCompanyId.Value;
            }

            // Professionals ARE allowed to create payments, but only for appointments they can access.
            // This is used by the Professional check-out flow.
            if (_currentUser.IsProfessional)
            {
                var appointmentId = TryExtractAppointmentId(dto.Reference);
                if (!appointmentId.HasValue)
                    throw new ForbiddenException("Para criar pagamento como profissional, a referência deve conter o id do agendamento (ex.: 'Appointment #123').");

                // Validates: same company + professional is assigned (directly or via team membership)
                await _scope.EnsureAppointmentAccessAsync(appointmentId.Value);

                // If customerId / addressId are missing (some UIs don't send them), infer from the appointment.
                var appt = await _unitOfWork.Appointments.GetById(appointmentId.Value);
                if (appt == null)
                    throw new InvalidOperationException("Agendamento não encontrado para vincular o pagamento.");

                if (!dto.CustomerId.HasValue)
                    dto.CustomerId = appt.CustomerId;

                if (!dto.CustomerAddressId.HasValue)
                    dto.CustomerAddressId = appt.CustomerAddressId;
            }

            // Validate customer belongs to the scoped company (for non-admins).
            if (!_currentUser.IsAdmin && dto.CustomerId.HasValue)
            {
                var customer = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId.Value);
                if (customer == null)
                    throw new InvalidOperationException("Cliente não encontrado para vincular o pagamento.");

                await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
            }

            int? customerAddressId = dto.CustomerAddressId;
            if (customerAddressId.HasValue)
            {
                var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(customerAddressId.Value);
                if (addr == null || (dto.CustomerId.HasValue && addr.CustomerId != dto.CustomerId.Value))
                    throw new InvalidOperationException("Endereço do cliente não encontrado para vincular o pagamento.");

                if (!dto.CustomerId.HasValue)
                    dto.CustomerId = addr.CustomerId;
            }
            else if (dto.CustomerId.HasValue)
            {
                var primary = await _unitOfWork.CustomerAddresses.GetPrimaryByCustomerAsync(dto.CustomerId.Value);
                customerAddressId = primary?.Id;
            }

            var entity = new Payment
            {
                CompanyId = dto.CompanyId,
                CustomerId = dto.CustomerId,
                CustomerAddressId = customerAddressId,
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

        private static int? TryExtractAppointmentId(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return null;

            // Expected formats from the frontend:
            // - "Appointment #2255"
            // - "Appointment #2255 - Customer Name"
            // Be forgiving with spaces/case.
            var match = Regex.Match(reference, @"appointment\s*#\s*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            if (int.TryParse(match.Groups[1].Value, out var id))
                return id;

            return null;
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

            if (dto.CustomerAddressId.HasValue)
            {
                var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(dto.CustomerAddressId.Value);
                if (addr == null)
                    throw new InvalidOperationException("Endereço do cliente não encontrado.");

                if (entity.CustomerId.HasValue && addr.CustomerId != entity.CustomerId.Value)
                    throw new InvalidOperationException("Endereço não pertence ao cliente informado.");

                entity.CustomerAddressId = dto.CustomerAddressId.Value;
                if (!entity.CustomerId.HasValue) entity.CustomerId = addr.CustomerId;
            }
            else if (dto.CustomerId.HasValue)
            {
                var primary = await _unitOfWork.CustomerAddresses.GetPrimaryByCustomerAsync(dto.CustomerId.Value);
                entity.CustomerAddressId = primary?.Id;
            }
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
