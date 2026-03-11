using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface IChecklistItemRepository : IGenericRepository<ChecklistItem>
    {
        Task<ChecklistItem?> GetWithPhotosAsync(int itemId);
    }

    public class ChecklistItemRepository : GenericRepository<ChecklistItem>, IChecklistItemRepository
    {
        public ChecklistItemRepository(DbContextClass context) : base(context) {}

        public Task<ChecklistItem?> GetWithPhotosAsync(int itemId) =>
            _dbContext.ChecklistItems
                .Include(i => i.Photos)
                .Include(i => i.CustomerArea)
                .Include(i => i.ChecklistTemplateItem)
                .FirstOrDefaultAsync(i => i.Id == itemId);
    }
}
