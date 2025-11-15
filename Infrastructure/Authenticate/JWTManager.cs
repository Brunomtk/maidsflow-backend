using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Core.DTO.User;
using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Authenticate
{
    public class JWTManager : IJWTManager
    {
        private readonly IConfiguration _configuration;

        public JWTManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<TokenJWT?> Authenticate(User user)
        {
            return Authenticate(user, rememberMe: false);
        }

        public Task<TokenJWT?> Authenticate(User user, bool rememberMe)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var key = _configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Jwt:Key is not configured.");

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            var authClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            // Define expiração do token de acesso
            int defaultDays = rememberMe ? 30 : 7;
            int days = _configuration.GetValue<int?>("Jwt:AccessTokenDays") ?? defaultDays;
            var expires = DateTime.UtcNow.AddDays(days);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(authClaims),
                Expires = expires,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
            };

            var handler = new JwtSecurityTokenHandler();
            var securityToken = handler.CreateToken(tokenDescriptor);
            var jwtToken = handler.WriteToken(securityToken);

            var refreshToken = GenerateRefreshToken();

            return Task.FromResult<TokenJWT?>(new TokenJWT
            {
                Token = jwtToken,
                RefreshToken = refreshToken
            });
        }

        private static string GenerateRefreshToken()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    public interface IJWTManager
    {
        Task<TokenJWT?> Authenticate(User user);
        Task<TokenJWT?> Authenticate(User user, bool rememberMe);
    }
}
