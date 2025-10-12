using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public interface IChecklistItemPhotoRepository : IGenericRepository<ChecklistItemPhoto>
    {
        Task<ChecklistItemPhoto?> GetByIdAsync(int id);
    }

    public class ChecklistItemPhotoRepository : GenericRepository<ChecklistItemPhoto>, IChecklistItemPhotoRepository
    {
        public ChecklistItemPhotoRepository(DbContextClass context) : base(context) {}

        public Task<ChecklistItemPhoto?> GetByIdAsync(int id) =>
            _dbContext.ChecklistItemPhotos.FirstOrDefaultAsync(p => p.Id == id);
    }
}
