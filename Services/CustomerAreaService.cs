using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Checklist;
using Core.Models;
using Infrastructure.Repositories;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public interface ICustomerAreaService
    {
        Task<CustomerArea?> CreateAsync(CreateCustomerAreaDTO dto);
        Task<bool> UpdateAsync(UpdateCustomerAreaDTO dto);
        Task<bool> SoftDeleteAsync(int id);
        IQueryable<CustomerArea> QueryByCustomer(int customerId, bool onlyActive);
    }

    public class CustomerAreaService : ICustomerAreaService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public CustomerAreaService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<CustomerArea?> CreateAsync(CreateCustomerAreaDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para criar áreas do cliente.");

            await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId);

            // Avoid duplicate active area names per customer
            if (await _uow.CustomerAreas.ExistsActiveByNameAsync(dto.CustomerId, dto.Name))
                return null;

            var area = new CustomerArea
            {
                CustomerId = dto.CustomerId,
                Name = dto.Name,
                Active = true
            };

            await _uow.CustomerAreas.Add(area);
            var saved = await _uow.SaveAsync() > 0;
            return saved ? area : null;
        }

        public async Task<bool> UpdateAsync(UpdateCustomerAreaDTO dto)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para editar áreas do cliente.");

            var area = await _uow.CustomerAreas.GetByIdAsync(dto.Id);
            if (area == null) return false;

            await _scope.EnsureCustomerInCompanyAsync(area.CustomerId);

            if (dto.Name != null)
            {
                // enforce uniqueness if changing name
                if (!string.Equals(area.Name, dto.Name, System.StringComparison.OrdinalIgnoreCase) &&
                    await _uow.CustomerAreas.ExistsActiveByNameAsync(area.CustomerId, dto.Name, excludeId: dto.Id))
                    return false;

                area.Name = dto.Name;
            }

            if (dto.Active.HasValue) area.Active = dto.Active.Value;

            _uow.CustomerAreas.Update(area);
            return await _uow.SaveAsync() > 0;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para remover áreas do cliente.");

            var area = await _uow.CustomerAreas.GetByIdAsync(id);
            if (area == null) return false;

            await _scope.EnsureCustomerInCompanyAsync(area.CustomerId);

            area.Active = false;
            _uow.CustomerAreas.Update(area);
            return await _uow.SaveAsync() > 0;
        }

        public IQueryable<CustomerArea> QueryByCustomer(int customerId, bool onlyActive)
        {
            // Queryable returning is used by controllers; still enforce scope for non-admin.
            // For professional: allow read inside company.
            if (!_currentUser.IsAdmin)
            {
                // throws if not in scope
                _scope.EnsureCustomerInCompanyAsync(customerId).GetAwaiter().GetResult();
            }

            return _uow.CustomerAreas.QueryByCustomer(customerId, onlyActive);
        }
    }
}
