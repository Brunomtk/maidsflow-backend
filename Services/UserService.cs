using Core.Enums.User;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.ServiceExtension;
using Core.DTO.User;
using Core.DTO;
using Core.Models; // ✅ IMPORTANTE para PagedResult<User>

namespace Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
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
            var user = await _unitOfWork.Users.GetById(userId);
            if (user != null)
            {
                user.Password = string.Empty; // ocultar hash de senha
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

        public async Task<bool> UpdateUser(CreateUserRequest userParam, int userId)
        {
            var user = await _unitOfWork.Users.GetById(userId);
            if (user == null) return false;

            user.Name = userParam.Name;
            user.Email = userParam.Email;
            user.Role = userParam.Role;
            user.Status = userParam.Status;
            user.CompanyId = userParam.CompanyId;
            user.ProfessionalId = userParam.ProfessionalId;
            user.Password = Encrypt.EncryptPassword(userParam.Password);

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
        Task<bool> UpdateUser(CreateUserRequest userParam, int userId);
        Task<bool> DeleteUser(int userId);
    }
}
