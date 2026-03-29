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
        Task<List<PushSubscriptionSummaryDTO>> GetMySubscriptionsAsync(int userId);
        Task<PushSubscriptionSummaryDTO> UpsertAsync(int userId, BrowserPushSubscriptionDTO dto);
        Task<bool> UnsubscribeAsync(int userId, string endpoint);
        Task<bool> MarkOpenedAsync(int userId, PushNotificationOpenedDTO dto);
        Task<PushSubscriptionSummaryDTO?> SendTestAsync(int userId, PushNotificationTestDTO dto);
    }
}
