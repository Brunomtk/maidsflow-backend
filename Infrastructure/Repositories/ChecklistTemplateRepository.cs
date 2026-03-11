using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface IChecklistTemplateRepository : IGenericRepository<ChecklistTemplate>
    {
        Task<ChecklistTemplate?> GetByIdWithItemsAsync(int id);
        Task<List<ChecklistTemplate>> GetVisibleTemplatesAsync(int? companyId);
        Task<bool> ExistsByNameAsync(int companyId, string name, int? excludeId = null);
    }

    public class ChecklistTemplateRepository : GenericRepository<ChecklistTemplate>, IChecklistTemplateRepository
    {
        public ChecklistTemplateRepository(DbContextClass context) : base(context) { }

        public Task<ChecklistTemplate?> GetByIdWithItemsAsync(int id) =>
            _dbContext.ChecklistTemplates
                .Include(x => x.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
                .FirstOrDefaultAsync(x => x.Id == id);

        public Task<List<ChecklistTemplate>> GetVisibleTemplatesAsync(int? companyId) =>
            _dbContext.ChecklistTemplates
                .AsNoTracking()
                .Include(x => x.Items)
                .Where(x => x.IsActive && (x.CompanyId == null || x.CompanyId == companyId))
                .OrderByDescending(x => x.IsSystemTemplate)
                .ThenBy(x => x.Name)
                .ToListAsync();

        public async Task<bool> ExistsByNameAsync(int companyId, string name, int? excludeId = null)
        {
            var q = _dbContext.ChecklistTemplates.Where(x => x.CompanyId == companyId && x.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            return await q.AnyAsync();
        }
    }
}
