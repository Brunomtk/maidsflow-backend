using System.Threading.Tasks;

namespace Services.Integrations.Guesty
{
    public class GuestyTokenResult
    {
        public required string AccessToken { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public string? Scope { get; set; }
        public string? ApiType { get; set; }
        public string? AuthBaseUrl { get; set; }
    }

    public interface IGuestyAuthClient
    {
        Task<GuestyTokenResult> GetTokenAsync(
            string apiType,
            string clientId,
            string clientSecret,
            string? authBaseUrl = null,
            string? scopeOverride = null);
    }
}
