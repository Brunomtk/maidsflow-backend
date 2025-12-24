using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.PushSubscriptions;
using Core.Models;

namespace Services
{
    /// <summary>
    /// Serviço de gerenciamento de subscriptions Web Push (PWA / navegador).
    /// </summary>
    public interface IPushSubscriptionService
    {
        Task<List<PushSubscription>> GetMySubscriptionsAsync(int userId);
        Task<PushSubscription> UpsertAsync(int userId, BrowserPushSubscriptionDTO dto);
        Task<bool> UnsubscribeAsync(int userId, string endpoint);
    }
}
