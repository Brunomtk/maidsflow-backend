using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IServiceTypeRepository : IGenericRepository<ServiceType>
    {
        Task<List<ServiceType>> GetByCompanyAsync(int companyId, bool includeInactive = false);
        Task<ServiceType?> GetByIdWithCompanyAsync(int id);
        Task<bool> ExistsByNameAsync(int companyId, string name, int? ignoreId = null);
    }

    public class ServiceTypeRepository : GenericRepository<ServiceType>, IServiceTypeRepository
    {
        public ServiceTypeRepository(DbContextClass context) : base(context) { }

        public async Task<List<ServiceType>> GetByCompanyAsync(int companyId, bool includeInactive = false)
        {
            var q = _dbContext.Set<ServiceType>()
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId);

            if (!includeInactive)
                q = q.Where(x => x.IsActive);

            return await q.OrderBy(x => x.Name).ToListAsync();
        }

        public async Task<ServiceType?> GetByIdWithCompanyAsync(int id)
        {
            return await _dbContext.Set<ServiceType>()
                .Include(x => x.Company)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<bool> ExistsByNameAsync(int companyId, string name, int? ignoreId = null)
        {
            var n = (name ?? string.Empty).Trim().ToLower();
            var q = _dbContext.Set<ServiceType>().AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.Name.ToLower() == n);

            if (ignoreId.HasValue)
                q = q.Where(x => x.Id != ignoreId.Value);

            return await q.AnyAsync();
        }
    }
}
