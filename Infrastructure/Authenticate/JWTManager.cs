using Core.DTO.User;
using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Authenticate
{
    public class JWTManager : IJWTManager
    {
        private readonly IConfiguration _configuration;

        public JWTManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<TokenJWT?> Authenticate(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenKey = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserId", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // Compute token expiration: prefer Jwt:AccessTokenDays, fallback to Jwt:AccessTokenMinutes, else 60 minutes
var accessDaysOpt = _configuration.GetValue<int?>("Jwt:AccessTokenDays");
var accessMinutesOpt = _configuration.GetValue<int?>("Jwt:AccessTokenMinutes");
DateTime expires;
if (accessDaysOpt.HasValue && accessDaysOpt.Value > 0)
{
    expires = DateTime.UtcNow.AddDays(accessDaysOpt.Value);
}
else if (accessMinutesOpt.HasValue && accessMinutesOpt.Value > 0)
{
    expires = DateTime.UtcNow.AddMinutes(accessMinutesOpt.Value);
}
else
{
    expires = DateTime.UtcNow.AddMinutes(60);
}
var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = _configuration["Jwt:Issuer"],      
                Audience = _configuration["Jwt:Audience"],   
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(tokenKey),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return new TokenJWT { Token = tokenHandler.WriteToken(token) };
        }
    
        private static string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    
public async Task<TokenJWT?> Authenticate(User user, bool rememberMe)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenKey = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "supersecretkey");
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var accessMinutes = int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var m) ? m : 60;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        // Always rotate refresh on auth with remember flag semantics handled by controller when persisting expiry
        var refresh = GenerateRefreshToken();

        return new TokenJWT { Token = tokenHandler.WriteToken(token), RefreshToken = refresh };
    }
}

public interface IJWTManager
    {
        Task<TokenJWT?> Authenticate(User user);
        Task<TokenJWT?> Authenticate(User user, bool rememberMe);
    }
}
