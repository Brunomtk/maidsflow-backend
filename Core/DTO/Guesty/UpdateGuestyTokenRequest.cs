using System;

namespace Core.DTO.Guesty
{
    public class UpdateGuestyTokenRequest
    {
        // Admin can override, otherwise current user's scoped company is used.
        public int? CompanyId { get; set; }

        // Option 1 (legacy): manually paste a token.
        public string? AccessToken { get; set; }
        public string? TokenType { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        // Option 2 (recommended): provide client credentials and let the backend generate / refresh the token.
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        // "bookingEngine" (default) or "openApi"
        public string? ApiType { get; set; }

        // Optional overrides
        public string? AuthBaseUrl { get; set; }
        public string? AuthScope { get; set; }
    }
}
