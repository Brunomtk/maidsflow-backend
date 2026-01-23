using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface ICustomerAreaRepository : IGenericRepository<CustomerArea>
    {
        Task<bool> ExistsActiveByNameAsync(int customerId, int? customerAddressId, string name, int? excludeId = null);
        Task<CustomerArea?> GetByIdAsync(int id);
        IQueryable<CustomerArea> QueryByCustomer(int customerId, int? customerAddressId, bool onlyActive);
    }

    public class CustomerAreaRepository : GenericRepository<CustomerArea>, ICustomerAreaRepository
    {
        public CustomerAreaRepository(DbContextClass context) : base(context) {}

        public Task<CustomerArea?> GetByIdAsync(int id) =>
            _dbContext.CustomerAreas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

        public IQueryable<CustomerArea> QueryByCustomer(int customerId, int? customerAddressId, bool onlyActive) =>
            _dbContext.CustomerAreas
                      .AsNoTracking()
                      .Where(a => a.CustomerId == customerId && a.CustomerAddressId == customerAddressId && (!onlyActive || a.Active))
                      .OrderBy(a => a.Name);

        public async Task<bool> ExistsActiveByNameAsync(int customerId, int? customerAddressId, string name, int? excludeId = null)
        {
            var q = _dbContext.CustomerAreas.Where(a => a.CustomerId == customerId &&
                                                        a.CustomerAddressId == customerAddressId &&
                                                        a.Active &&
                                                        a.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue) q = q.Where(a => a.Id != excludeId.Value);
            return await q.AnyAsync();
        }
    }
}
