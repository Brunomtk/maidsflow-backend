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
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());
        }

        /// <summary>
        /// Retorna usuários paginados com filtro por nome (opcional)
        /// </summary>
        public async Task<PagedResult<User>> GetAllUsuariosPaged(FiltersDTO filtersDTO)
        {
            return await _dbContext.Set<User>()
                .AsNoTracking()
                .Where(x =>
                    string.IsNullOrEmpty(filtersDTO.Name) ||
                    EF.Functions.Like(x.Name.ToLower(), $"%{filtersDTO.Name.ToLower()}%")
                )
                .GetPagedAsync(filtersDTO.pageNumber, filtersDTO.pageSize);
        }
    }

    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetUserByEmail(string email);
        Task<PagedResult<User>> GetAllUsuariosPaged(FiltersDTO filtersDTO);
    }
}
