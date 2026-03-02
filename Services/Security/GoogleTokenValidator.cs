using System;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace Services.Security
{
    public class GoogleTokenPayload
    {
        public string Subject { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
    }

    public interface IGoogleTokenValidator
    {
        Task<GoogleTokenPayload> ValidateIdTokenAsync(string idToken);
    }

    public class GoogleTokenValidator : IGoogleTokenValidator
    {
        private readonly IConfiguration _config;

        public GoogleTokenValidator(IConfiguration config)
        {
            _config = config;
        }

        public async Task<GoogleTokenPayload> ValidateIdTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                throw new ArgumentException("idToken is required", nameof(idToken));

            var clientId = _config["Auth:Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("Missing config Auth:Google:ClientId");

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleTokenPayload
            {
                Subject = payload.Subject ?? string.Empty,
                Email = (payload.Email ?? string.Empty).Trim().ToLowerInvariant(),
                EmailVerified = payload.EmailVerified,
                Name = payload.Name ?? payload.GivenName ?? string.Empty,
                Picture = payload.Picture ?? string.Empty
            };
        }
    }
}
