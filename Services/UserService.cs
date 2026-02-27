using Core.Enums.User;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.ServiceExtension;
using Core.DTO.User;
using Core.DTO;
using Core.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Exceptions;
using Services.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public UserService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        // --------------------
        // Auth helpers (used by UsersController authenticate/refresh-token)
        // Keep these permissive because refresh-token can be AllowAnonymous.
        // --------------------
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

        // --------------------
        // CRUD / queries
        // --------------------
        public async Task<bool> CreateUser(User user)
        {
            if (user == null) return false;

            // Normalize email early (enforce case-insensitive uniqueness)
            if (!string.IsNullOrWhiteSpace(user.Email))
                user.Email = user.Email.Trim().ToLower();

            // --------------------
            // Anonymous signup support
            // --------------------
            // During public signup the caller is not authenticated, so CurrentUser.UserId == 0.
            // In that flow we only allow creating the *first* company owner user (role=company)
            // for an already created CompanyId. Any other kind of user creation remains protected.
            if (_currentUser.UserId == 0)
            {
                // Only company owner can be created anonymously
                if (string.IsNullOrWhiteSpace(user.Role) ||
                    !user.Role.Equals("company", StringComparison.OrdinalIgnoreCase))
                    throw new ForbiddenException("Você não tem permissão para criar usuários.");

                // Must target a valid company
                if (!user.CompanyId.HasValue || user.CompanyId.Value <= 0)
                    throw new ForbiddenException("CompanyId inválido.");

                var companyExists = await _unitOfWork.Companies.GetById(user.CompanyId.Value);
                if (companyExists == null)
                    throw new ForbiddenException("CompanyId inválido.");

                // Anonymous signup cannot assign permissions nor link a professional
                if (user.ProfessionalId.HasValue)
                    throw new ForbiddenException("ProfessionalId não é permitido no signup.");

                if (user.Permissions != null && user.Permissions.Any())
                    throw new ForbiddenException("Permissões não são permitidas no signup.");

                if (string.IsNullOrWhiteSpace(user.Email))
                    throw new BadRequestException("E-mail é obrigatório.");

                if (await _unitOfWork.Users.EmailExistsAsync(user.Email))
                    throw new ConflictException("Esse e-mail já está em uso. Faça login ou use outro e-mail.");

                user.Password = Encrypt.EncryptPassword(user.Password);
                try
                {
                    await _unitOfWork.Users.Add(user);
                    var createdRows = _unitOfWork.Save();
                    return createdRows > 0;
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    throw new ConflictException("Esse e-mail já está em uso. Faça login ou use outro e-mail.");
                }
            }

            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para criar usuários.");

            if (_currentUser.IsCompany)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue)
                    throw new ForbiddenException("Escopo de company inválido.");

                // Company can only create users in its own company
                user.CompanyId = scopedCompanyId.Value;

                // Company cannot create admin users
                if (!string.IsNullOrWhiteSpace(user.Role) && user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                    throw new ForbiddenException("Company não pode criar usuário admin.");

                // Property Manager: must be tied to a single customer within the same company
                if (!string.IsNullOrWhiteSpace(user.Role) && user.Role.Equals("propertyManager", StringComparison.OrdinalIgnoreCase))
                {
                    if (!user.CustomerId.HasValue || user.CustomerId.Value <= 0)
                        throw new BadRequestException("Para role 'propertyManager', CustomerId é obrigatório.");

                    // must belong to this company
                    await _scope.EnsureCustomerInCompanyAsync(user.CustomerId.Value);

                    // property managers should not be linked to professionals
                    user.ProfessionalId = null;
                }

                if (user.ProfessionalId.HasValue)
                    await _scope.EnsureProfessionalInCompanyAsync(user.ProfessionalId.Value);
            }

            // Admin validations for Property Manager
            if (_currentUser.IsAdmin && !string.IsNullOrWhiteSpace(user.Role) && user.Role.Equals("propertyManager", StringComparison.OrdinalIgnoreCase))
            {
                if (!user.CustomerId.HasValue || user.CustomerId.Value <= 0)
                    throw new BadRequestException("Para role 'propertyManager', CustomerId é obrigatório.");

                var customer = await _unitOfWork.Customers.GetByIdAsync(user.CustomerId.Value);
                if (customer == null)
                    throw new NotFoundException("Cliente não encontrado.");

                // Ensure the user is always in the same company as the linked customer
                user.CompanyId = customer.CompanyId;
                user.ProfessionalId = null;
            }

            user.Password = Encrypt.EncryptPassword(user.Password);

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new BadRequestException("E-mail é obrigatório.");

            if (await _unitOfWork.Users.EmailExistsAsync(user.Email))
                throw new ConflictException("Esse e-mail já está em uso. Faça login ou use outro e-mail.");

            try
            {
                await _unitOfWork.Users.Add(user);
                var result = _unitOfWork.Save();
                return result > 0;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                throw new ConflictException("Esse e-mail já está em uso. Faça login ou use outro e-mail.");
            }
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            // Postgres unique violation: 23505
            if (ex.InnerException is PostgresException pg)
                return pg.SqlState == PostgresErrorCodes.UniqueViolation;
            return false;
        }

        public async Task<bool> DeleteUser(int userId)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para remover usuários.");

            var user = await _unitOfWork.Users.GetById(userId);
            if (user == null) return false;

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureCompanyAccessAsync(user.CompanyId ?? 0);

                if (!string.IsNullOrWhiteSpace(user.Role) && user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                    throw new ForbiddenException("Company não pode remover usuário admin.");
            }

            _unitOfWork.Users.Delete(user);
            var result = _unitOfWork.Save();

            return result > 0;
        }

        public async Task<IEnumerable<User>> GetAllUsers()
        {
            var users = await _unitOfWork.Users.GetAll();

            if (_currentUser.IsAdmin) return users;

            if (_currentUser.IsCompany || _currentUser.IsProfessional)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (!scopedCompanyId.HasValue) return Enumerable.Empty<User>();

                if (_currentUser.IsProfessional)
                {
                    // Professional sees only itself
                    return users.Where(u => u.Id == _currentUser.UserId);
                }

                return users.Where(u => u.CompanyId == scopedCompanyId.Value);
            }

            return Enumerable.Empty<User>();
        }

        public async Task<PagedResult<User>> GetAllUsuariosPaged(FiltersDTO filtersDTO)
        {
            // Apply scope into filters
            if (!_currentUser.IsAdmin)
            {
                var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
                if (scopedCompanyId.HasValue)
                    filtersDTO.CompanyId = scopedCompanyId.Value;

                if (_currentUser.IsProfessional)
                    filtersDTO.ProfessionalId = await _scope.GetScopedProfessionalIdAsync();
            }

            return await _unitOfWork.Users.GetAllUsuariosPaged(filtersDTO);
        }

        public async Task<User?> GetUserById(int userId)
        {
            // Carrega o usuário já com as permissões incluídas
            var user = await _unitOfWork.Users.GetByIdWithPermissions(userId);
            if (user == null) return null;

            if (_currentUser.IsAdmin)
            {
                user.Password = string.Empty;
                return user;
            }

            if (_currentUser.IsProfessional)
            {
                await _scope.EnsureUserSelfOrAdminAsync(userId);
                user.Password = string.Empty;
                return user;
            }

            if (_currentUser.IsCompany)
            {
                // company can only access users of its own company
                if (!user.CompanyId.HasValue)
                    throw new ForbiddenException("Usuário sem company.");

                await _scope.EnsureCompanyAccessAsync(user.CompanyId.Value);
                user.Password = string.Empty;
                return user;
            }

            throw new ForbiddenException();
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;

            var user = await _unitOfWork.Users.GetUserByEmail(email);
            return user;
        }

        public async Task<User?> GetUserByEmailForUpdate(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return await _unitOfWork.Users.GetUserByEmailForUpdate(email);
        }

        public async Task<User?> GetByPasswordResetTokenHash(string tokenHash)
        {
            if (string.IsNullOrWhiteSpace(tokenHash)) return null;
            return await _unitOfWork.Users.GetByPasswordResetTokenHash(tokenHash);
        }

        public async Task SaveAsync()
        {
            await _unitOfWork.SaveAsync();
        }

        public async Task<bool> UpdateUser(UpdateUserRequest userParam, int userId)
        {
            // Carrega usuário com permissões incluídas
            var user = await _unitOfWork.Users.GetByIdWithPermissions(userId);
            if (user == null) return false;

            if (_currentUser.IsProfessional)
            {
                // professional can only update itself and only profile fields / password / onboarding
                await _scope.EnsureUserSelfOrAdminAsync(userId);

                if (userParam.Name != null) user.Name = userParam.Name;
                if (userParam.Email != null) user.Email = userParam.Email;
                if (userParam.Onboarding.HasValue) user.Onboarding = userParam.Onboarding.Value;
                if (!string.IsNullOrEmpty(userParam.Password))
                    user.Password = Encrypt.EncryptPassword(userParam.Password);

                // cannot change role/company/professional/permissions/status
            }
            else if (_currentUser.IsCompany)
            {
                // company can only update users from its own company
                if (!user.CompanyId.HasValue)
                    throw new ForbiddenException("Usuário sem company.");

                await _scope.EnsureCompanyAccessAsync(user.CompanyId.Value);

                if (userParam.Name != null) user.Name = userParam.Name;
                if (userParam.Email != null) user.Email = userParam.Email;

                if (userParam.Status.HasValue) user.Status = userParam.Status.Value;
                if (userParam.Onboarding.HasValue) user.Onboarding = userParam.Onboarding.Value;

                if (!string.IsNullOrEmpty(userParam.Password))
                    user.Password = Encrypt.EncryptPassword(userParam.Password);

                // Role: company cannot promote to admin
                if (userParam.Role != null)
                {
                    if (userParam.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                        throw new ForbiddenException("Company não pode definir role admin.");

                    user.Role = userParam.Role;
                }

                // CompanyId cannot be changed by company users
                // ProfessionalId can be set, but must belong to this company
                if (userParam.ProfessionalId.HasValue)
                {
                    await _scope.EnsureProfessionalInCompanyAsync(userParam.ProfessionalId.Value);
                    user.ProfessionalId = userParam.ProfessionalId;
                }

                // CustomerId: only used for Property Manager role
                if (userParam.CustomerId.HasValue)
                {
                    // can only assign customer when role is propertyManager (current or being set)
                    var effectiveRole = userParam.Role ?? user.Role;
                    if (!string.Equals(effectiveRole, "propertyManager", StringComparison.OrdinalIgnoreCase))
                        throw new BadRequestException("CustomerId só pode ser definido para role 'propertyManager'.");

                    await _scope.EnsureCustomerInCompanyAsync(userParam.CustomerId.Value);
                    user.CustomerId = userParam.CustomerId;
                    user.ProfessionalId = null;
                }

                // If role is propertyManager ensure CustomerId exists (either existing or provided)
                if (string.Equals(user.Role, "propertyManager", StringComparison.OrdinalIgnoreCase))
                {
                    if (!user.CustomerId.HasValue || user.CustomerId.Value <= 0)
                        throw new BadRequestException("Para role 'propertyManager', CustomerId é obrigatório.");

                    await _scope.EnsureCustomerInCompanyAsync(user.CustomerId.Value);
                    user.ProfessionalId = null;
                }

                // Permissions: allowed for company
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
            }
            else if (_currentUser.IsAdmin)
            {
                if (userParam.Name != null) user.Name = userParam.Name;
                if (userParam.Email != null) user.Email = userParam.Email;
                if (userParam.Role != null) user.Role = userParam.Role;
                if (userParam.Status.HasValue) user.Status = userParam.Status.Value;

                if (userParam.CompanyId.HasValue) user.CompanyId = userParam.CompanyId;
                if (userParam.ProfessionalId.HasValue) user.ProfessionalId = userParam.ProfessionalId;

                // CustomerId: only meaningful for Property Manager role
                if (userParam.CustomerId.HasValue)
                {
                    var effectiveRole = userParam.Role ?? user.Role;
                    if (!string.Equals(effectiveRole, "propertyManager", StringComparison.OrdinalIgnoreCase))
                        throw new BadRequestException("CustomerId só pode ser definido para role 'propertyManager'.");

                    user.CustomerId = userParam.CustomerId;
                }
                if (userParam.Onboarding.HasValue) user.Onboarding = userParam.Onboarding.Value;

                // Admin validations for Property Manager
                if (string.Equals(user.Role, "propertyManager", StringComparison.OrdinalIgnoreCase))
                {
                    if (!user.CustomerId.HasValue || user.CustomerId.Value <= 0)
                        throw new BadRequestException("Para role 'propertyManager', CustomerId é obrigatório.");

                    var customer = await _unitOfWork.Customers.GetByIdAsync(user.CustomerId.Value);
                    if (customer == null)
                        throw new NotFoundException("Cliente não encontrado.");

                    // Ensure the user is always in the same company as the linked customer
                    user.CompanyId = customer.CompanyId;
                    user.ProfessionalId = null;
                }
                else
                {
                    // If role changed away from propertyManager, keep data clean
                    if (userParam.Role != null && !string.Equals(userParam.Role, "propertyManager", StringComparison.OrdinalIgnoreCase))
                        user.CustomerId = null;
                }

                if (!string.IsNullOrEmpty(userParam.Password))
                    user.Password = Encrypt.EncryptPassword(userParam.Password);

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

            }
            else
            {
                throw new ForbiddenException();
            }

            _unitOfWork.Users.Update(user);
            var result = _unitOfWork.Save();

            return result > 0;
        }

        public async Task<bool> UpdateUserPreferences(int userId, string? language, string? theme)
        {
            // self or admin
            if (!_currentUser.IsAdmin)
                await _scope.EnsureUserSelfOrAdminAsync(userId);

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
        Task<User?> GetUserByEmailForUpdate(string email);
        Task<User?> GetByPasswordResetTokenHash(string tokenHash);
        Task SaveAsync();

        Task<bool> UpdateUser(UpdateUserRequest userParam, int userId);
        Task<bool> DeleteUser(int userId);
        Task<bool> UpdateRefreshToken(int userId, string? refreshToken, DateTime? expiresAt);
        Task<User?> GetByRefreshToken(string refreshToken);
        Task<bool> UpdateUserPreferences(int userId, string? language, string? theme);
    }
}
