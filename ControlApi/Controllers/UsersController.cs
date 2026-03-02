using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
using Services.Security;
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
        private readonly ICompanyService _companyService;
        private readonly IConfiguration _configuration;
        private readonly IGoogleTokenValidator _googleTokenValidator;
        private readonly IS3StorageService _s3;
        private readonly ICredentialsEmailService _credentialsEmail;
        private readonly IPasswordResetEmailService _passwordResetEmail;

        
        public UsersController(
            IJWTManager jwtManager,
            IUserService userService,
            IConfiguration configuration,
            IS3StorageService s3,
            ICredentialsEmailService credentialsEmail,
            IPasswordResetEmailService passwordResetEmail,
            IGoogleTokenValidator googleTokenValidator,
            ICompanyService companyService)
        {
            _jwtManager = jwtManager;
            _userService = userService;
            _companyService = companyService;
            _configuration = configuration;
            _s3 = s3;
            _credentialsEmail = credentialsEmail;
            _passwordResetEmail = passwordResetEmail;
            _googleTokenValidator = googleTokenValidator;
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
        [HttpPost("authenticate/google")]
                public async Task<IActionResult> AuthenticateGoogle([FromBody] GoogleAuthenticateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
                return BadRequest("IdToken is required");

            GoogleTokenPayload payload;
            try
            {
                payload = await _googleTokenValidator.ValidateIdTokenAsync(request.IdToken);
            }
            catch (Exception ex)
            {
                return Unauthorized($"Invalid Google token: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(payload.Email))
                return Unauthorized("Google token does not contain a valid email");

            // Prefer existing user by email (case-insensitive).
            var user = await _userService.GetUserByEmail(payload.Email);

            if (user == null)
            {
                // Create a new user (no password login). We store a random password to satisfy the legacy schema.
                var randomPassword = Guid.NewGuid().ToString("N") + "!";
                user = new User
                {
                    Name = string.IsNullOrWhiteSpace(request.Name) ? (string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name) : request.Name!,
                    Email = payload.Email,
                    Password = randomPassword,
                    Role = "company",
                    Status = Core.Enums.StatusEnum.Active,
                    CompanyId = null,
                    ProfessionalId = null,
                    CustomerId = null,
                    Onboarding = false,
                    Language = "pt-BR",
                    Theme = "dark",
                    Avatar = payload.Picture
                };

                var ok = await _userService.CreateUser(user);
                if (!ok)
                    return BadRequest("Unable to create user");

                // Reload to get Id / persisted fields (CreateUser encrypts password)
                user = await _userService.GetUserByEmail(payload.Email);
                if (user == null)
                    return BadRequest("User creation failed");
            }

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
                Onboarding = user.Onboarding
            };

            return Ok(response);
        }[HttpPost("refresh-token")]
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

        
// ===== FORGOT / RESET PASSWORD =====

public class ForgotPasswordAnonymousRequest
{
    public string Email { get; set; } = string.Empty;
    public string? WebBaseUrl { get; set; }
}

[AllowAnonymous]
[HttpPost("forgot-password")]
public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordAnonymousRequest request)
{
    if (request == null || string.IsNullOrWhiteSpace(request.Email))
        return Ok(new ForgotPasswordResponse { Ok = true });

    // Always return OK (do not leak whether the email exists)
    var email = request.Email.Trim();

    var user = await _userService.GetUserByEmail(email);
    if (user == null)
        return Ok(new ForgotPasswordResponse { Ok = true });

    // Generate token (store only hash)
    var tokenBytes = RandomNumberGenerator.GetBytes(32);
    var token = Base64UrlEncode(tokenBytes);

    var tokenHash = Sha256Hex(token);
    var expiresAt = DateTime.UtcNow.AddHours(1);

    // Need tracked user to update
    var tracked = await _userService.GetUserByEmailForUpdate(email);
    if (tracked != null)
    {
        tracked.PasswordResetTokenHash = tokenHash;
        tracked.PasswordResetTokenExpiresAt = expiresAt;
        await _userService.SaveAsync();
    }

    // Build reset URL
    var baseUrl = request.WebBaseUrl;
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        // fallback: use the request origin (may be the API domain)
        baseUrl = $"{Request.Scheme}://{Request.Host}";
    }

    var resetUrl = $"{baseUrl!.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

    try
    {
        await _passwordResetEmail.SendPasswordResetEmailAsync(user.Id, resetUrl, HttpContext.RequestAborted);
    }
    catch
    {
        // Do not fail the request if email provider fails
    }

    return Ok(new ForgotPasswordResponse { Ok = true });
}

public class ResetPasswordAnonymousRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string? LoginUrl { get; set; }
}

[AllowAnonymous]
[HttpPost("reset-password")]
public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordAnonymousRequest request)
{
    if (request == null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        return BadRequest("Token and NewPassword are required.");

    if (request.NewPassword.Length < 6)
        return BadRequest("Password must be at least 6 characters.");

    var tokenHash = Sha256Hex(request.Token.Trim());

    var user = await _userService.GetByPasswordResetTokenHash(tokenHash);
    if (user == null)
        return BadRequest("Invalid token.");

    if (!user.PasswordResetTokenExpiresAt.HasValue || user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
        return BadRequest("Token expired.");

    user.Password = Encrypt.EncryptPassword(request.NewPassword);
    user.PasswordResetTokenHash = null;
    user.PasswordResetTokenExpiresAt = null;

    // revoke refresh token to force re-login
    user.RefreshToken = null;
    user.RefreshTokenExpiresAt = null;

    await _userService.SaveAsync();

    // Best-effort notice email (reuse existing service)
    try
    {
        await _credentialsEmail.SendPasswordChangedNoticeAsync(
            userId: user.Id,
            loginUrl: request.LoginUrl,
            ct: HttpContext.RequestAborted);
    }
    catch
    {
    }

    return Ok(new ResetPasswordResponse { Ok = true });
}

private static string Sha256Hex(string value)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes);
}

private static string Base64UrlEncode(byte[] bytes)
{
    return Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
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


        [Authorize]
        [HttpPut("me/link-company")]
        public async Task<IActionResult> LinkCompanyToCurrentUser([FromBody] LinkCompanyRequest request)
        {
        if (request == null || request.CompanyId <= 0)
        return BadRequest("CompanyId is required");

        var userIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userId = int.TryParse(userIdClaim, out var uid) ? uid : 0;
        if (userId <= 0)
        return Unauthorized();

        var user = await _userService.GetUserById(userId);
        if (user == null)
        return NotFound("User not found");

        // Only company owner accounts can be linked
        if (!string.Equals(user.Role, "company", StringComparison.OrdinalIgnoreCase))
        return Forbid("Only company users can be linked to a company");

        // Prevent accidental reassignment
        if (user.CompanyId.HasValue && user.CompanyId.Value > 0 && user.CompanyId.Value != request.CompanyId)
        return Forbid("User is already linked to a different company");

        // Validate target company exists
        var companyOk = await _companyService.CompanyExists(request.CompanyId);
            if (!companyOk)
                return BadRequest("Invalid CompanyId");

        // Link and persist
        user.CompanyId = request.CompanyId;

        var ok = await _userService.UpdateUserCompany(user.Id, request.CompanyId);
        if (!ok)
        return BadRequest("Unable to link company");

        return Ok(new { success = true, companyId = request.CompanyId });
        }
    }
}