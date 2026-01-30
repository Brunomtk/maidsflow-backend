using System.Threading.Tasks;
using Core.DTO.Guesty;

namespace Services.Integrations.Guesty
{
    public interface IGuestyCustomerAddressSyncService
    {
        Task<GuestySyncCustomerAddressesResultDTO> SyncCustomerAddressesAsync(GuestySyncCustomerAddressesRequest request);
    }
}
