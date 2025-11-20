using Core.Enums.User;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.ServiceExtension;
using Core.DTO.User;
using Core.DTO;
using Core.Models; // ✅ IMPORTANTE para PagedResult<User>
using System.Linq;

namespace Services
{
    public class UserService : IUserService
    {
        public async Task<User?> GetByRefreshToken(string refreshToken)
        {
            return await _unitOfWork.Users.GetByRefreshToken(refreshToken);
        }

        public async Task<bool> UpdateRefreshToken(int userId, string? refreshToken, DateTime? expiresAt)
        {
            var user = await _unitOfWork.Users.GetById(userId);
            if (user == null) return false;
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiresAt = expiresAt;
            await _unitOfWork.SaveAsync();
            return true;
        }

        private readonly Infrastructure.Repositories.IUnitOfWork _unitOfWork;

        public UserService(Infrastructure.Repositories.IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> CreateUser(User user)
        {
            if (user == null) return false;

            user.Password = Encrypt.EncryptPassword(user.Password);
            await _unitOfWork.Users.Add(user);

            var result = _unitOfWork.Save();
            return result > 0;
        }

        public async Task<bool> DeleteUser(int userId)
        {
            var user = await _unitOfWork.Users.GetById(userId);
            if (user == null) return false;

            _unitOfWork.Users.Delete(user);
            var result = _unitOfWork.Save();

            return result > 0;
        }

        public async Task<IEnumerable<User>> GetAllUsers()
        {
            return await _unitOfWork.Users.GetAll();
        }

        public async Task<PagedResult<User>> GetAllUsuariosPaged(FiltersDTO filtersDTO)
        {
            return await _unitOfWork.Users.GetAllUsuariosPaged(filtersDTO);
        }

        public async Task<User?> GetUserById(int userId)
        {
            // Carrega o usuário já com as permissões incluídas
            var user = await _unitOfWork.Users.GetByIdWithPermissions(userId);
            if (user != null)
            {
                // Nunca retornar o hash da senha
                user.Password = string.Empty;
                return user;
            }

            return null;
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;

            var user = await _unitOfWork.Users.GetUserByEmail(email);
            return user;
        }

        public async Task<bool> UpdateUser(UpdateUserRequest userParam, int userId)
        {
            // Carrega usuário com permissões incluídas
            var user = await _unitOfWork.Users.GetByIdWithPermissions(userId);
            if (user == null) return false;

            if (userParam.Name != null)
                user.Name = userParam.Name;

            if (userParam.Email != null)
                user.Email = userParam.Email;

            if (userParam.Role != null)
                user.Role = userParam.Role;

            if (userParam.Status.HasValue)
                user.Status = userParam.Status.Value;

            if (userParam.CompanyId.HasValue)
                user.CompanyId = userParam.CompanyId;

            if (userParam.ProfessionalId.HasValue)
                user.ProfessionalId = userParam.ProfessionalId;

            if (!string.IsNullOrEmpty(userParam.Password))
                user.Password = Encrypt.EncryptPassword(userParam.Password);

            // Se a lista de permissões vier preenchida, substitui as permissões atuais
            if (userParam.Permissions != null)
            {
                user.Permissions.Clear();

                foreach (var perm in userParam.Permissions)
                {
                    user.Permissions.Add(new UserPermission
                    {
                        UserId = user.Id,
                        Code = perm.Code,
                        Description = perm.Description
                    });
                }
            }

            _unitOfWork.Users.Update(user);
            var result = _unitOfWork.Save();

            return result > 0;
        }

        public async Task<bool> UpdateUserPreferences(int userId, string? language, string? theme)
        {
            var user = await _unitOfWork.Users.GetById(userId);
            if (user == null) return false;

            if (!string.IsNullOrWhiteSpace(language))
                user.Language = language;

            if (!string.IsNullOrWhiteSpace(theme))
                user.Theme = theme;

            _unitOfWork.Users.Update(user);
            var result = _unitOfWork.Save();

            return result > 0;
        }
    }

    public interface IUserService
    {
        Task<bool> CreateUser(User user);
        Task<IEnumerable<User>> GetAllUsers();
        Task<PagedResult<User>> GetAllUsuariosPaged(FiltersDTO filtersDTO);
        Task<User?> GetUserById(int userId);
        Task<User?> GetUserByEmail(string email);
        Task<bool> UpdateUser(UpdateUserRequest userParam, int userId);
        Task<bool> DeleteUser(int userId);
        Task<bool> UpdateRefreshToken(int userId, string? refreshToken, DateTime? expiresAt);
        Task<User?> GetByRefreshToken(string refreshToken);
        Task<bool> UpdateUserPreferences(int userId, string? language, string? theme);
    }
}
