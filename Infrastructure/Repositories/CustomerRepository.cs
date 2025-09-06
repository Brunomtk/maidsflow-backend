using Core.DTO.Customer;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface ICustomerRepository : IGenericRepository<Customer>
    {
        Task<Customer?> GetByIdAsync(int id); // ✅ Corrigido para int
        Task<PagedResult<Customer>> GetPagedCustomersAsync(CustomerFiltersDTO filtersDTO);
    }

    public class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
    {
        private readonly DbContextClass _context;

        public CustomerRepository(DbContextClass context) : base(context)
        {
            _context = context;
        }

        public async Task<Customer?> GetByIdAsync(int id) // ✅ Corrigido para int
        {
            return await _context.Customers
                .Include(c => c.Company)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<PagedResult<Customer>> GetPagedCustomersAsync(CustomerFiltersDTO filtersDTO)
        {
            var query = _context.Customers
                .Include(c => c.Company)
                .AsQueryable();

            // ✅ Corrigido para tipo int
            if (filtersDTO.CompanyId.HasValue && filtersDTO.CompanyId.Value > 0)
                query = query.Where(c => c.CompanyId == filtersDTO.CompanyId.Value);

            if (!string.IsNullOrWhiteSpace(filtersDTO.Name))
                query = query.Where(c => c.Name.Contains(filtersDTO.Name));

            if (!string.IsNullOrWhiteSpace(filtersDTO.Ssn))
                query = query.Where(c => c.Ssn != null && c.Ssn.Contains(filtersDTO.Ssn));

            if (filtersDTO.Status.HasValue)
                query = query.Where(c => c.Status == filtersDTO.Status.Value);

            query = query.OrderByDescending(c => c.CreatedDate);

            return await query.GetPagedAsync(filtersDTO.PageNumber, filtersDTO.PageSize);
        }
    }
}
