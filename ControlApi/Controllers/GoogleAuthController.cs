using System;
using System.Threading.Tasks;
using Core.DTO.User;
using Core.Models;
using Infrastructure.Authenticate;
using Infrastructure.Security;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.Security;
using Services.Storage;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/Users")]
    public class GoogleAuthController : ControllerBase
    {
        private readonly IJWTManager _jwtManager;
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly IS3StorageService _s3;
        private readonly DbContextClass _db;
        private readonly IGoogleTokenValidator _googleTokenValidator;

        public GoogleAuthController(
            IJWTManager jwtManager,
            IUserService userService,
            IConfiguration configuration,
            IS3StorageService s3,
            DbContextClass db,
            IGoogleTokenValidator googleTokenValidator)
        {
            _jwtManager = jwtManager;
            _userService = userService;
            _configuration = configuration;
            _s3 = s3;
            _db = db;
            _googleTokenValidator = googleTokenValidator;
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

            var user = await _userService.GetUserByEmail(payload.Email);

            if (user == null)
            {
                
var randomPassword = Guid.NewGuid().ToString("N") + "!";
user = new User
{
    Name = string.IsNullOrWhiteSpace(request.Name) ? (string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name) : request.Name!,
    Email = payload.Email,
    Password = Encrypt.EncryptPassword(randomPassword),
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

await _db.Users.AddAsync(user);
await _db.SaveChangesAsync();

// Reload to ensure all fields are populated
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
        }
    }
}
