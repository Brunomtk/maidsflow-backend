using System.Threading.Tasks;
using Core.DTO.Guesty;

namespace Services.Integrations.Guesty
{
    public interface IGuestyIntegrationService
    {
        Task<GuestyIntegrationStatusDTO> GetStatusAsync(int? companyIdOverride = null);
        Task<GuestyIntegrationStatusDTO> UpdateTokenAsync(UpdateGuestyTokenRequest request);
        Task ClearTokenAsync(int? companyIdOverride = null);

        // Returns bearer token string or throws if missing/expired.
        Task<string> GetAccessTokenOrThrowAsync(int? companyIdOverride = null);
    }
}
