using Core.DTO.User;
using Core.DTO;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Infrastructure.ServiceExtension;
using Core.Models; // ✅ Necessário para GetPagedAsync e PagedResult

namespace Infrastructure.Repositories
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(DbContextClass dbContext) : base(dbContext)
        {
        }

        /// <summary>
        /// Retorna um usuário pelo e-mail (usado no login e validações)
        /// </summary>
        public async Task<User?> GetUserByEmail(string email)
        {
            return await _dbContext.Set<User>()
                .Include(u => u.Permissions)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());
        }

        /// <summary>
        /// Retorna usuários paginados com filtro por nome (opcional)
        /// </summary>
        public async Task<PagedResult<User>> GetAllUsuariosPaged(FiltersDTO filtersDTO)
        {
            return await _dbContext.Set<User>()
                .Include(u => u.Permissions)
                .AsNoTracking()
                .Where(x =>
                    (string.IsNullOrEmpty(filtersDTO.Name) || EF.Functions.Like(x.Name.ToLower(), $"%{filtersDTO.Name.ToLower()}%"))
                    && (!filtersDTO.CompanyId.HasValue || x.CompanyId == filtersDTO.CompanyId)
                    && (!filtersDTO.ProfessionalId.HasValue || x.ProfessionalId == filtersDTO.ProfessionalId)
                )
                .GetPagedAsync(filtersDTO.pageNumber, filtersDTO.pageSize);
        }



        public async Task<User?> GetByIdWithPermissions(int id)
        {
            return await _dbContext.Set<User>()
                .Include(u => u.Permissions)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

    public async Task<User?> GetByRefreshToken(string refreshToken)
    {
        return await _dbContext.Set<User>()
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);
    }
}

public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetUserByEmail(string email);
        Task<PagedResult<User>> GetAllUsuariosPaged(FiltersDTO filtersDTO);
        Task<User?> GetByRefreshToken(string refreshToken);
        Task<User?> GetByIdWithPermissions(int id);
    }
}
