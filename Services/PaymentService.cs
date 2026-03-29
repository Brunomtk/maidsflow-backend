using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.DTO.Payments;
using Core.Enums.Payment;
using Core.Enums.Notifications;
using Core.Enums.User;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Services.Security;

namespace Services
{
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

    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public PaymentService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<Payment>> GetPagedAsync(PaymentFiltersDto filters)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to access payments.");

            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (companyId.HasValue)
                {
                    filters.CompanyId = companyId.Value;
                    await EnsureDefaultCategoryIfPossibleAsync(companyId.Value);
                }
            }
            else if (filters.CompanyId.HasValue)
            {
                await EnsureDefaultCategoryIfPossibleAsync(filters.CompanyId.Value);
            }

            return await _unitOfWork.Payments.GetPagedAsync(filters);
        }

        public async Task<Payment?> GetByIdAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to access payments.");

            var payment = await _unitOfWork.Payments.GetByIdAsync(id);
            if (payment == null) return null;

            if (_currentUser.IsPropertyManager)
            {
                if (!payment.CustomerId.HasValue)
                    throw new ForbiddenException("Payment has no linked customer.");

                await _scope.EnsureCustomerInCompanyAsync(payment.CustomerId.Value);
            }

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(payment.CompanyId);

            return payment;
        }

        public async Task<List<Payment>> GetByCustomer(int customerId)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to access payments.");

            if (_currentUser.IsPropertyManager)
            {
                await _scope.EnsureCustomerInCompanyAsync(customerId);
                return await _unitOfWork.Payments.GetByCustomerIdAsync(customerId);
            }

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
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (scopedCompanyId.HasValue)
                    dto.CompanyId = scopedCompanyId.Value;
            }

            if (_currentUser.IsProfessional)
            {
                var appointmentId = TryExtractAppointmentId(dto.Reference);
                if (!appointmentId.HasValue)
                    throw new ForbiddenException("To create a payment as a professional user, the reference must contain the appointment id (e.g. 'Appointment #123').");

                await _scope.EnsureAppointmentAccessAsync(appointmentId.Value);

                var appt = await _unitOfWork.Appointments.GetById(appointmentId.Value);
                if (appt == null)
                    throw new InvalidOperationException("Appointment not found to link the payment.");

                if (!dto.CustomerId.HasValue)
                    dto.CustomerId = appt.CustomerId;

                if (!dto.CustomerAddressId.HasValue)
                    dto.CustomerAddressId = appt.CustomerAddressId;

                dto.FinancialType = PaymentFinancialType.Income;
                if (!dto.PaymentCategoryId.HasValue && string.IsNullOrWhiteSpace(dto.PaymentCategoryName))
                    dto.PaymentCategoryName = PaymentCategoryService.DefaultCategoryName;
            }

            if (!_currentUser.IsAdmin && dto.CustomerId.HasValue)
            {
                var customer = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId.Value);
                if (customer == null)
                    throw new InvalidOperationException("Customer not found to link the payment.");

                await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
            }

            int? customerAddressId = dto.CustomerAddressId;
            if (customerAddressId.HasValue)
            {
                var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(customerAddressId.Value);
                if (addr == null || (dto.CustomerId.HasValue && addr.CustomerId != dto.CustomerId.Value))
                    throw new InvalidOperationException("Customer address not found to link the payment.");

                if (!dto.CustomerId.HasValue)
                    dto.CustomerId = addr.CustomerId;
            }
            else if (dto.CustomerId.HasValue)
            {
                var primary = await _unitOfWork.CustomerAddresses.GetPrimaryByCustomerAsync(dto.CustomerId.Value);
                customerAddressId = primary?.Id;
            }

            var category = await ResolveCategoryAsync(dto.CompanyId, dto.PaymentCategoryId, dto.PaymentCategoryName, dto.FinancialType);

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
                FinancialType = dto.FinancialType,
                PaymentCategoryId = category?.Id,
                PaymentCategoryName = category?.Name,
                PlanId = dto.PlanId,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.Payments.Add(entity);
            await _unitOfWork.SaveAsync();
            await CreateCompanyPaymentNotificationAsync(entity, isStatusNotification: false, action: "created");
            return entity;
        }

        public async Task<Payment?> UpdateAsync(int id, UpdatePaymentDto dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to edit payments.");

            var entity = await _unitOfWork.Payments.GetByIdAsync(id);
            if (entity == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

            if (_currentUser.IsAdmin && dto.CompanyId.HasValue)
                entity.CompanyId = dto.CompanyId.Value;

            if (dto.CustomerId.HasValue)
                entity.CustomerId = dto.CustomerId.Value;

            if (dto.CustomerAddressId.HasValue)
            {
                var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(dto.CustomerAddressId.Value);
                if (addr == null)
                    throw new InvalidOperationException("Customer address not found.");

                if (entity.CustomerId.HasValue && addr.CustomerId != entity.CustomerId.Value)
                    throw new InvalidOperationException("The address does not belong to the selected customer.");

                entity.CustomerAddressId = dto.CustomerAddressId.Value;
                if (!entity.CustomerId.HasValue)
                    entity.CustomerId = addr.CustomerId;
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
            if (!string.IsNullOrWhiteSpace(dto.Reference)) entity.Reference = dto.Reference;
            if (dto.PlanId.HasValue) entity.PlanId = dto.PlanId.Value;
            if (dto.FinancialType.HasValue) entity.FinancialType = dto.FinancialType.Value;

            if (dto.PaymentCategoryId.HasValue || !string.IsNullOrWhiteSpace(dto.PaymentCategoryName) || dto.FinancialType.HasValue)
            {
                var category = await ResolveCategoryAsync(entity.CompanyId, dto.PaymentCategoryId, dto.PaymentCategoryName, entity.FinancialType);
                entity.PaymentCategoryId = category?.Id;
                entity.PaymentCategoryName = category?.Name;
            }

            entity.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.Payments.Update(entity);
            await _unitOfWork.SaveAsync();

            var shouldNotifyUpdate = dto.Amount.HasValue
                || dto.DueDate.HasValue
                || dto.PaymentDate.HasValue
                || dto.Status.HasValue
                || dto.FinancialType.HasValue
                || dto.PaymentCategoryId.HasValue
                || !string.IsNullOrWhiteSpace(dto.PaymentCategoryName)
                || !string.IsNullOrWhiteSpace(dto.Reference);

            if (shouldNotifyUpdate)
                await CreateCompanyPaymentNotificationAsync(entity, isStatusNotification: dto.Status.HasValue, action: dto.Status.HasValue ? "status-updated" : "updated");

            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to delete payments.");

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
                throw new ForbiddenException("Professional users do not have permission to process payments.");

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
            await CreateCompanyPaymentNotificationAsync(entity, isStatusNotification: true, action: "status-updated");
            return entity;
        }

        private async Task CreateCompanyPaymentNotificationAsync(Payment payment, bool isStatusNotification, string action)
        {
            if (payment.CompanyId <= 0)
                return;

            var title = BuildNotificationTitle(payment, isStatusNotification);
            var message = BuildNotificationMessage(payment, action);

            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = isStatusNotification ? NotificationType.Success : NotificationType.Info,
                RecipientId = 0,
                RecipientRole = UserRole.Company,
                CompanyId = payment.CompanyId,
                UserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
                Status = NotificationStatus.Unread,
                SentAt = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.Notifications.Add(notification);
            await _unitOfWork.SaveAsync();
        }

        private static string BuildNotificationTitle(Payment payment, bool isStatusNotification)
        {
            var kind = payment.FinancialType == PaymentFinancialType.Expense ? "Accounts payable" : "Accounts receivable";
            if (isStatusNotification)
                return $"{kind} updated";

            return $"New {kind.ToLowerInvariant()} entry";
        }

        private static string BuildNotificationMessage(Payment payment, string action)
        {
            var kind = payment.FinancialType == PaymentFinancialType.Expense ? "accounts payable" : "accounts receivable";
            var category = string.IsNullOrWhiteSpace(payment.PaymentCategoryName) ? "Uncategorized" : payment.PaymentCategoryName;
            var reference = string.IsNullOrWhiteSpace(payment.Reference) ? $"entry #{payment.Id}" : payment.Reference;
            var dueDate = payment.DueDate.ToString("MM/dd/yyyy");
            var amount = payment.Amount.ToString("0.00");

            return action switch
            {
                "created" => $"A new {kind} was created: {reference}. Category: {category}. Amount: {amount}. Due date: {dueDate}.",
                "status-updated" => $"A {kind} {reference} was updated to status {payment.Status}. Category: {category}. Amount: {amount}.",
                _ => $"A {kind} {reference} was updated. Category: {category}. Amount: {amount}. Due date: {dueDate}."
            };
        }

        private async Task<PaymentCategory?> ResolveCategoryAsync(int companyId, int? paymentCategoryId, string? paymentCategoryName, PaymentFinancialType financialType)
        {
            await EnsureDefaultCategoryIfPossibleAsync(companyId);

            if (paymentCategoryId.HasValue)
            {
                var byId = await _unitOfWork.PaymentCategories.GetByIdAsync(paymentCategoryId.Value);
                if (byId == null || byId.CompanyId != companyId)
                    throw new InvalidOperationException("Payment category not found for the specified company.");
                return byId;
            }

            var normalized = string.IsNullOrWhiteSpace(paymentCategoryName)
                ? (financialType == PaymentFinancialType.Income ? PaymentCategoryService.DefaultCategoryName : null)
                : paymentCategoryName.Trim();

            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var existing = await _unitOfWork.PaymentCategories.GetByCompanyIdAndNameAsync(companyId, normalized);
            if (existing != null) return existing;

            var created = new PaymentCategory
            {
                CompanyId = companyId,
                Name = normalized,
                Active = true,
                IsSystem = string.Equals(normalized, PaymentCategoryService.DefaultCategoryName, StringComparison.OrdinalIgnoreCase),
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _unitOfWork.PaymentCategories.Add(created);
            await _unitOfWork.SaveAsync();
            return created;
        }

        private async Task EnsureDefaultCategoryIfPossibleAsync(int companyId)
        {
            var existing = await _unitOfWork.PaymentCategories.GetByCompanyIdAndNameAsync(companyId, PaymentCategoryService.DefaultCategoryName);
            if (existing != null) return;

            var category = new PaymentCategory
            {
                CompanyId = companyId,
                Name = PaymentCategoryService.DefaultCategoryName,
                Active = true,
                IsSystem = true,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _unitOfWork.PaymentCategories.Add(category);
            await _unitOfWork.SaveAsync();
        }

        private static int? TryExtractAppointmentId(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return null;

            var match = Regex.Match(reference, @"appointment\s*#\s*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            return int.TryParse(match.Groups[1].Value, out var id) ? id : null;
        }
    }
}
