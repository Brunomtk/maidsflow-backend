using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface ICustomerAddressRepository : IGenericRepository<CustomerAddress>
    {
        Task<CustomerAddress?> GetByIdAsync(int id);
        Task<List<CustomerAddress>> GetByCustomerAsync(int customerId);
        Task<CustomerAddress?> GetPrimaryByCustomerAsync(int customerId);
    }

    public class CustomerAddressRepository : GenericRepository<CustomerAddress>, ICustomerAddressRepository
    {
        private readonly DbContextClass _context;

        public CustomerAddressRepository(DbContextClass context) : base(context)
        {
            _context = context;
        }

        public async Task<CustomerAddress?> GetByIdAsync(int id)
        {
            return await _context.CustomerAddresses
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<CustomerAddress>> GetByCustomerAsync(int customerId)
        {
            return await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.IsPrimary)
                .ThenByDescending(a => a.CreatedDate)
                .ToListAsync();
        }

        public async Task<CustomerAddress?> GetPrimaryByCustomerAsync(int customerId)
        {
            return await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId && a.IsPrimary)
                .OrderByDescending(a => a.CreatedDate)
                .FirstOrDefaultAsync();
        }
    }
}
