using Core.DTO.Payments;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;

namespace Services
{
    public interface IPaymentCategoryService
    {
        Task<List<PaymentCategory>> GetAllAsync(bool includeInactive = false);
        Task<PaymentCategory?> GetByIdAsync(int id);
        Task<PaymentCategory> CreateAsync(CreatePaymentCategoryDto dto);
        Task<PaymentCategory?> UpdateAsync(int id, UpdatePaymentCategoryDto dto);
        Task<bool> DeleteAsync(int id);
        Task<PaymentCategory> EnsureDefaultCategoryAsync(int companyId);
    }

    public class PaymentCategoryService : IPaymentCategoryService
    {
        public const string DefaultCategoryName = "Appointments";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public PaymentCategoryService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<List<PaymentCategory>> GetAllAsync(bool includeInactive = false)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to access payment categories.");

            var companyId = await ResolveScopedCompanyIdAsync();
            await EnsureDefaultCategoryAsync(companyId);
            return await _unitOfWork.PaymentCategories.GetByCompanyIdAsync(companyId, includeInactive);
        }

        public async Task<PaymentCategory?> GetByIdAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to access payment categories.");

            var entity = await _unitOfWork.PaymentCategories.GetByIdAsync(id);
            if (entity == null) return null;
            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);
            return entity;
        }

        public async Task<PaymentCategory> CreateAsync(CreatePaymentCategoryDto dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to create payment categories.");

            var companyId = await ResolveScopedCompanyIdAsync();
            await EnsureDefaultCategoryAsync(companyId);

            var name = NormalizeName(dto.Name);
            var existing = await _unitOfWork.PaymentCategories.GetByCompanyIdAndNameAsync(companyId, name);
            if (existing != null) return existing;

            var entity = new PaymentCategory
            {
                CompanyId = companyId,
                Name = name,
                Active = dto.Active,
                IsSystem = false,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.PaymentCategories.Add(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<PaymentCategory?> UpdateAsync(int id, UpdatePaymentCategoryDto dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to edit payment categories.");

            var entity = await _unitOfWork.PaymentCategories.GetByIdAsync(id);
            if (entity == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var normalized = NormalizeName(dto.Name);
                var duplicate = await _unitOfWork.PaymentCategories.GetByCompanyIdAndNameAsync(entity.CompanyId, normalized);
                if (duplicate != null && duplicate.Id != entity.Id)
                    throw new InvalidOperationException("A payment category with this name already exists.");
                entity.Name = normalized;
            }

            if (dto.Active.HasValue)
            {
                if (entity.IsSystem && dto.Active.Value == false)
                    throw new InvalidOperationException("The default Appointments category cannot be deactivated.");
                entity.Active = dto.Active.Value;
            }

            entity.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.PaymentCategories.Update(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional users do not have permission to delete payment categories.");

            var entity = await _unitOfWork.PaymentCategories.GetByIdAsync(id);
            if (entity == null) return false;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId);

            if (entity.IsSystem)
                throw new InvalidOperationException("The default Appointments category cannot be removed.");

            var hasPayments = await _unitOfWork.PaymentCategories.HasPaymentsAsync(id);
            if (hasPayments)
                throw new InvalidOperationException("This category cannot be removed because it is already used by financial entries.");

            _unitOfWork.PaymentCategories.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<PaymentCategory> EnsureDefaultCategoryAsync(int companyId)
        {
            var existing = await _unitOfWork.PaymentCategories.GetByCompanyIdAndNameAsync(companyId, DefaultCategoryName);
            if (existing != null)
            {
                if (!existing.Active || !existing.IsSystem)
                {
                    existing.Active = true;
                    existing.IsSystem = true;
                    existing.UpdatedDate = DateTime.UtcNow;
                    _unitOfWork.PaymentCategories.Update(existing);
                    await _unitOfWork.SaveAsync();
                }
                return existing;
            }

            var entity = new PaymentCategory
            {
                CompanyId = companyId,
                Name = DefaultCategoryName,
                Active = true,
                IsSystem = true,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _unitOfWork.PaymentCategories.Add(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        private async Task<int> ResolveScopedCompanyIdAsync()
        {
            if (_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (companyId.HasValue) return companyId.Value;
                throw new InvalidOperationException("CompanyId could not be resolved for the payment category.");
            }

            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new InvalidOperationException("CompanyId could not be resolved for the payment category.");
            return scopedCompanyId.Value;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Payment category name is required.");
            return value.Trim();
        }
    }
}
