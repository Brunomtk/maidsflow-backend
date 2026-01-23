using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Checklist;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface IChecklistRepository : IGenericRepository<Checklist>
    {
        Task<Checklist?> GetByIdWithItemsAsync(int id);
        Task<PagedResult<Checklist>> GetPagedAsync(ChecklistFiltersDTO filters);
    }

    public class ChecklistRepository : GenericRepository<Checklist>, IChecklistRepository
    {
        public ChecklistRepository(DbContextClass context) : base(context) {}

        public Task<Checklist?> GetByIdWithItemsAsync(int id) =>
            _dbContext.Checklists
                .Include(c => c.CustomerAddress)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Photos)
                .FirstOrDefaultAsync(c => c.Id == id);

        public async Task<PagedResult<Checklist>> GetPagedAsync(ChecklistFiltersDTO filters)
        {
            IQueryable<Checklist> q = _dbContext.Checklists.AsNoTracking();

            if (filters.CustomerId.HasValue) q = q.Where(c => c.CustomerId == filters.CustomerId.Value);
            if (filters.CustomerAddressId.HasValue) q = q.Where(c => c.CustomerAddressId == filters.CustomerAddressId.Value);
            if (filters.CompanyId.HasValue) q = q.Where(c => c.CompanyId == filters.CompanyId.Value);
            if (filters.Status.HasValue) q = q.Where(c => c.Status.ToString() == filters.Status.Value.ToString());
            if (filters.AppointmentId.HasValue) q = q.Where(c => c.AppointmentId == filters.AppointmentId.Value);
            if (filters.ProfessionalId.HasValue) q = q.Where(c => c.ProfessionalId == filters.ProfessionalId.Value);
            if (filters.From.HasValue) q = q.Where(c => c.CreatedDate >= filters.From.Value);
            if (filters.To.HasValue) q = q.Where(c => c.CreatedDate <= filters.To.Value);

            q = q.OrderByDescending(c => c.CreatedDate);
            return await q.GetPagedAsync(filters.PageNumber, filters.PageSize);
        }
    }
}
