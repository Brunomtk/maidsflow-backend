using Core.Models;

namespace Infrastructure.Repositories
{
    public interface IChecklistTemplateItemRepository : IGenericRepository<ChecklistTemplateItem>
    {
    }

    public class ChecklistTemplateItemRepository : GenericRepository<ChecklistTemplateItem>, IChecklistTemplateItemRepository
    {
        public ChecklistTemplateItemRepository(DbContextClass context) : base(context) { }
    }
}
