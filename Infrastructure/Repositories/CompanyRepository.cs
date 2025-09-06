using Core.DTO;
using Core.DTO.Company;
using Core.Enums;
using Core.Models;
using Infrastructure.ServiceExtension;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class CompanyRepository : GenericRepository<Company>, ICompanyRepository
    {
        private readonly DbContextClass _dbContext;

        public CompanyRepository(DbContextClass dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Gets a company by CNPJ.
        /// </summary>
        public async Task<Company?> GetByCnpj(string cnpj)
        {
            return await _dbContext.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Cnpj == cnpj);
        }

        /// <summary>
        /// Gets a company by ID.
        /// </summary>
        public async Task<Company?> GetByIdAsync(int companyId)
        {
            return await _dbContext.Companies
                .AsNoTracking()
                .Include(c => c.Plan)
                .FirstOrDefaultAsync(c => c.Id == companyId);
        }

        /// <summary>
        /// Gets the PlanId associated with a given CompanyId.
        /// </summary>
        public async Task<int?> GetPlanIdByCompanyId(int companyId)
        {
            return await _dbContext.Companies
                .Where(c => c.Id == companyId)
                .Select(c => (int?)c.PlanId)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Returns paged companies filtered by name, plan, and status.
        /// </summary>
        public async Task<PagedResult<Company>> GetCompaniesPagedFilteredAsync(CompanyFiltersDTO filters)
        {
            var query = _dbContext.Companies
                .Include(c => c.Plan)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filters.Name))
            {
                var nameLower = filters.Name.ToLower();
                query = query.Where(c =>
                    EF.Functions.Like(c.Name.ToLower(), $"%{nameLower}%") ||
                    (c.Cnpj != null && c.Cnpj.Contains(filters.Name))
                );
            }

            if (filters.PlanId.HasValue)
            {
                query = query.Where(c => c.PlanId == filters.PlanId.Value);
            }

            if (!string.IsNullOrEmpty(filters.Status) && filters.Status.ToLower() != "all")
            {
                StatusEnum? statusEnum = filters.Status.ToLower() switch
                {
                    "ativo" => StatusEnum.Active,
                    "inativo" => StatusEnum.Inactive,
                    _ => null
                };

                if (statusEnum.HasValue)
                    query = query.Where(c => c.Status == statusEnum.Value);
            }

            return await query
                .OrderByDescending(c => c.CreatedDate)
                .GetPagedAsync(filters.Page, filters.PageSize);
        }
    }

    public interface ICompanyRepository : IGenericRepository<Company>
    {
        Task<Company?> GetByCnpj(string cnpj);
        Task<Company?> GetByIdAsync(int companyId);
        Task<int?> GetPlanIdByCompanyId(int companyId);
        Task<PagedResult<Company>> GetCompaniesPagedFilteredAsync(CompanyFiltersDTO filters);
    }
}
