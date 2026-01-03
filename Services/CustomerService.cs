using Core.DTO.Customer;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Threading.Tasks;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public interface ICustomerService
    {
        Task<Customer?> GetByIdAsync(int id);
        Task<PagedResult<Customer>> GetPagedAsync(CustomerFiltersDTO filters);
        Task<Customer?> CreateAsync(Customer customer);
        Task<bool> UpdateAsync(Customer customer);
        Task<bool> DeleteAsync(int id);
    }

    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public CustomerService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<Customer?> GetByIdAsync(int id)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(id);
            if (customer == null) return null;

            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);
            return customer;
        }

        public async Task<PagedResult<Customer>> GetPagedAsync(CustomerFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                filters.CompanyId = scopedCompanyId.Value;
            }

            return await _unitOfWork.Customers.GetPagedCustomersAsync(filters);
        }

        public async Task<Customer?> CreateAsync(Customer customer)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para criar clientes.");

            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                customer.CompanyId = scopedCompanyId.Value;
            }

            await _unitOfWork.Customers.Add(customer);
            var result = await _unitOfWork.SaveAsync();
            return result > 0 ? customer : null;
        }

        public async Task<bool> UpdateAsync(Customer customer)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para editar clientes.");

            var existing = await _unitOfWork.Customers.GetByIdAsync(customer.Id);
            if (existing == null) return false;

            await _scope.EnsureCompanyAccessAsync(existing.CompanyId);

            // company users cannot move customers between companies
            if (!_currentUser.IsAdmin)
                customer.CompanyId = existing.CompanyId;

            _unitOfWork.Customers.Update(customer);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para remover clientes.");

            var existing = await _unitOfWork.Customers.GetByIdAsync(id);
            if (existing == null) return false;

            await _scope.EnsureCompanyAccessAsync(existing.CompanyId);

            _unitOfWork.Customers.Delete(existing);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }
    }
}
