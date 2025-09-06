using Core.DTO.Professional;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class ProfessionalRepository : GenericRepository<Professional>, IProfessionalRepository
    {
        private readonly DbContextClass _dbContext;

        public ProfessionalRepository(DbContextClass dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Professional>> GetAllAsync()
        {
            return await _dbContext.Professionals
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Professional?> GetByIdAsync(int id)
        {
            return await _dbContext.Professionals
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PagedResult<Professional>> GetPagedProfessionalsAsync(ProfessionalFiltersDTO filters)
        {
            var query = _dbContext.Professionals
                .AsNoTracking()
                .AsQueryable();

            if (filters.CompanyId.HasValue)
                query = query.Where(p => p.CompanyId == filters.CompanyId.Value);

            if (filters.TeamId.HasValue)
                query = query.Where(p => p.TeamId == filters.TeamId.Value);

            if (filters.LeaderId.HasValue)
            {
                query = query.Where(p =>
                    _dbContext.Teams.Any(t => t.Id == p.TeamId && t.LeaderId == filters.LeaderId.Value)
                );
            }

            return await query.GetPagedAsync(filters.Page, filters.PageSize);
        }
    }

    public interface IProfessionalRepository : IGenericRepository<Professional>
    {
        Task<List<Professional>> GetAllAsync();
        Task<Professional?> GetByIdAsync(int id);
        Task<PagedResult<Professional>> GetPagedProfessionalsAsync(ProfessionalFiltersDTO filters);
    }
}
