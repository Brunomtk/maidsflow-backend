using Core.Enums.Team;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public interface IPayrollRuleRepository : IGenericRepository<PayrollRule>
    {
        Task<List<PayrollRule>> GetByCompanyAsync(int companyId, bool includeInactive = false);
        Task<PayrollRule?> GetByIdWithRelationsAsync(int id);
        Task<bool> ExistsRuleAsync(int companyId, int? serviceTypeId, TeamMemberRole teamRole, int priority, int? ignoreId = null);
    }

    public class PayrollRuleRepository : GenericRepository<PayrollRule>, IPayrollRuleRepository
    {
        public PayrollRuleRepository(DbContextClass context) : base(context) { }

        public async Task<List<PayrollRule>> GetByCompanyAsync(int companyId, bool includeInactive = false)
        {
            var q = _dbContext.Set<PayrollRule>()
                .AsNoTracking()
                .Include(r => r.ServiceType)
                .Where(r => r.CompanyId == companyId);

            if (!includeInactive)
                q = q.Where(r => r.IsActive);

            return await q
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.TeamRole)
                .ThenBy(r => r.ServiceTypeId)
                .ToListAsync();
        }

        public async Task<PayrollRule?> GetByIdWithRelationsAsync(int id)
        {
            return await _dbContext.Set<PayrollRule>()
                .Include(r => r.Company)
                .Include(r => r.ServiceType)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<bool> ExistsRuleAsync(int companyId, int? serviceTypeId, TeamMemberRole teamRole, int priority, int? ignoreId = null)
        {
            var q = _dbContext.Set<PayrollRule>()
                .AsNoTracking()
                .Where(r => r.CompanyId == companyId && r.TeamRole == teamRole && r.Priority == priority);

            if (serviceTypeId.HasValue)
                q = q.Where(r => r.ServiceTypeId == serviceTypeId.Value);
            else
                q = q.Where(r => r.ServiceTypeId == null);

            if (ignoreId.HasValue)
                q = q.Where(r => r.Id != ignoreId.Value);

            return await q.AnyAsync();
        }
    }
}
