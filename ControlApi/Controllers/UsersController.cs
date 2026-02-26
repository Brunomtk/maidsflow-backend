using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO;
using Core.DTO.User;
using Core.Models;
using Infrastructure.Authenticate;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Services;
using Services.Email;
using Services.Storage;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IJWTManager _jwtManager;
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly IS3StorageService _s3;
        private readonly ICredentialsEmailService _credentialsEmail;

        public UsersController(IJWTManager jwtManager, IUserService userService, IConfiguration configuration, IS3StorageService s3, ICredentialsEmailService credentialsEmail)
        {
            _jwtManager = jwtManager;
            _userService = userService;
            _configuration = configuration;
            _s3 = s3;
            _credentialsEmail = credentialsEmail;
        }

        // ===== AUTENTICAÇÃO =====

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] LoginRequest login)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userService.GetUserByEmail(login.Email);
            if (user == null)
                return Unauthorized("Invalid credentials");

            if (user.Password != Encrypt.EncryptPassword(login.Password))
                return Unauthorized("Invalid credentials");

            var token = await _jwtManager.Authenticate(user, login.RememberMe);
            if (token == null)
                return Unauthorized("Token generation failed");

            var daysKey = login.RememberMe ? "Jwt:RememberMeRefreshDays" : "Jwt:RefreshTokenDays";
            var days = _configuration.GetValue<int?>(daysKey) ?? 30;
            await _userService.UpdateRefreshToken(
                user.Id,
                token.RefreshToken,
                DateTime.UtcNow.AddDays(days)
            );

            var avatarUrl = !string.IsNullOrWhiteSpace(user.AvatarKey)
                ? _s3.CreateDownloadUrl(user.AvatarKey)
                : user.Avatar;

            var response = new AuthUserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Avatar = avatarUrl,
                AvatarKey = user.AvatarKey,
                AvatarUrl = avatarUrl,
                Status = user.Status,
                Token = token.Token,
                RefreshToken = token.RefreshToken,
                CompanyId = user.CompanyId,
                ProfessionalId = user.ProfessionalId,
                CustomerId = user.CustomerId,
                Language = user.Language,
                Theme = user.Theme,
                Onboarding = user.Onboarding,
                CreatedDate = user.CreatedDate,
                UpdatedDate = user.UpdatedDate,
                Permissions = user.Permissions != null
                    ? user.Permissions.Select(p => p.Code.ToString()).ToList()
                    : new List<string>()
            };

            return Ok(response);
        }

        public class RefreshTokenRequest
        {
            public string RefreshToken { get; set; } = string.Empty;
            public bool RememberMe { get; set; } = true;
        }

        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest("RefreshToken is required");

            var user = await _userService.GetByRefreshToken(request.RefreshToken);
            if (user == null || (user.RefreshTokenExpiresAt.HasValue && user.RefreshTokenExpiresAt.Value < DateTime.UtcNow))
                return Unauthorized("Invalid refresh token");

            var token = await _jwtManager.Authenticate(user, request.RememberMe);
            if (token == null)
                return Unauthorized("Token generation failed");

            var daysKey = request.RememberMe ? "Jwt:RememberMeRefreshDays" : "Jwt:RefreshTokenDays";
            var days = _configuration.GetValue<int?>(daysKey) ?? 30;
            await _userService.UpdateRefreshToken(
                user.Id,
                token.RefreshToken,
                DateTime.UtcNow.AddDays(days)
            );

            var avatarUrl = !string.IsNullOrWhiteSpace(user.AvatarKey)
                ? _s3.CreateDownloadUrl(user.AvatarKey)
                : user.Avatar;

            var response = new AuthUserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Avatar = avatarUrl,
                AvatarKey = user.AvatarKey,
                AvatarUrl = avatarUrl,
                Status = user.Status,
                Token = token.Token,
                RefreshToken = token.RefreshToken,
                CompanyId = user.CompanyId,
                ProfessionalId = user.ProfessionalId,
                CustomerId = user.CustomerId,
                Language = user.Language,
                Theme = user.Theme,
                Onboarding = user.Onboarding,
                CreatedDate = user.CreatedDate,
                UpdatedDate = user.UpdatedDate,
                Permissions = user.Permissions != null
                    ? user.Permissions.Select(p => p.Code.ToString()).ToList()
                    : new List<string>()
            };

            return Ok(response);
        }

        // ===== CRUD BÁSICO =====

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? name,
            [FromQuery] int? companyId,
            [FromQuery] int? professionalId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var filters = new FiltersDTO
            {
                Name = name,
                CompanyId = companyId,
                ProfessionalId = professionalId,
                pageNumber = pageNumber,
                pageSize = pageSize
            };

            var result = await _userService.GetAllUsuariosPaged(filters);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userService.GetUserById(id);
            if (user == null)
                return NotFound();

            return Ok(user);
        }
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Password = request.Password,
                Role = request.Role,
                Status = request.Status,
                CompanyId = request.CompanyId,
                ProfessionalId = request.ProfessionalId,
                CustomerId = request.CustomerId,
                Language = request.Language,
                Theme = request.Theme,
                Onboarding = request.Onboarding,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            // Mapeia permissões (opcionais) vindas da request
            if (request.Permissions != null && request.Permissions.Any())
            {
                user.Permissions = request.Permissions
                    .Select(p => new UserPermission
                    {
                        Code = p.Code,
                        Description = p.Description
                    })
                    .ToList();
            }

            var created = await _userService.CreateUser(user);
            if (!created)
                return BadRequest("Could not create user");

            return Ok(user);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _userService.UpdateUser(request, id);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _userService.DeleteUser(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        public class UpdatePreferencesRequest
        {
            public string? Language { get; set; }
            public string? Theme { get; set; }
        }

        [HttpPut("{id:int}/preferences")]
        public async Task<IActionResult> UpdatePreferences(int id, [FromBody] UpdatePreferencesRequest request)
        {
            var ok = await _userService.UpdateUserPreferences(id, request.Language, request.Theme);
            if (!ok)
                return NotFound();

            return NoContent();
        }

        // ===== CREDENTIALS EMAIL (SendGrid) =====

        [HttpPost("{id:int}/send-credentials")]
        public async Task<IActionResult> SendCredentialsEmail(int id, [FromBody] SendCredentialsEmailRequest request)
        {
            request ??= new SendCredentialsEmailRequest();

            var result = await _credentialsEmail.SendUserCredentialsAsync(
                userId: id,
                generateNewPassword: request.GenerateNewPassword,
                loginUrl: request.LoginUrl,
                ct: HttpContext.RequestAborted);

            return Ok(new SendCredentialsEmailResponse
            {
                UserId = result.UserId,
                ToEmail = result.ToEmail,
                PasswordRegenerated = result.PasswordRegenerated,
                GeneratedPassword = result.GeneratedPassword,
                EmailSent = result.EmailSent,
                ProviderStatusCode = result.ProviderStatusCode,
                ProviderResponse = result.ProviderResponse
            });
        }

        [HttpPost("{id:int}/send-password-changed-notice")]
        public async Task<IActionResult> SendPasswordChangedNotice(int id, [FromBody] SendPasswordChangedNoticeRequest request)
        {
            request ??= new SendPasswordChangedNoticeRequest();

            var result = await _credentialsEmail.SendPasswordChangedNoticeAsync(
                userId: id,
                loginUrl: request.LoginUrl,
                ct: HttpContext.RequestAborted);

            return Ok(new SendPasswordChangedNoticeResponse
            {
                UserId = result.UserId,
                ToEmail = result.ToEmail,
                EmailSent = result.EmailSent,
                ProviderStatusCode = result.ProviderStatusCode,
                ProviderResponse = result.ProviderResponse
            });
        }
    }
}
