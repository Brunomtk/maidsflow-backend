using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class LeaderRepository : GenericRepository<Leader>, ILeaderRepository
    {
        private readonly DbContextClass _dbContext;

        public LeaderRepository(DbContextClass dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Leader?> GetByIdAsync(int id)
        {
            return await _dbContext.Leaders
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<IEnumerable<Leader>> GetAllAsync()
        {
            return await _dbContext.Leaders
                .Include(l => l.User)
                .ToListAsync();
        }
    }

    public interface ILeaderRepository : IGenericRepository<Leader>
    {
        Task<Leader?> GetByIdAsync(int id);
        Task<IEnumerable<Leader>> GetAllAsync();
    }
}
