using Core.DTO.Customer;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using System.Threading.Tasks;

namespace Services
{
    public interface ICustomerService
    {
        Task<Customer?> GetByIdAsync(int id); // ✅ Corrigido de Guid para int
        Task<PagedResult<Customer>> GetPagedAsync(CustomerFiltersDTO filters);
        Task<Customer?> CreateAsync(Customer customer);
        Task<bool> UpdateAsync(Customer customer);
        Task<bool> DeleteAsync(int id); // ✅ Corrigido de Guid para int
    }

    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Customer?> GetByIdAsync(int id) // ✅ Corrigido de Guid para int
        {
            return await _unitOfWork.Customers.GetByIdAsync(id);
        }

        public async Task<PagedResult<Customer>> GetPagedAsync(CustomerFiltersDTO filters)
        {
            return await _unitOfWork.Customers.GetPagedCustomersAsync(filters);
        }

        public async Task<Customer?> CreateAsync(Customer customer)
        {
            await _unitOfWork.Customers.Add(customer);
            var result = await _unitOfWork.SaveAsync();
            return result > 0 ? customer : null;
        }

        public async Task<bool> UpdateAsync(Customer customer)
        {
            _unitOfWork.Customers.Update(customer);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }

        public async Task<bool> DeleteAsync(int id) // ✅ Corrigido de Guid para int
        {
            var existing = await _unitOfWork.Customers.GetByIdAsync(id);
            if (existing == null) return false;

            _unitOfWork.Customers.Delete(existing);
            var result = await _unitOfWork.SaveAsync();
            return result > 0;
        }
    }
}
