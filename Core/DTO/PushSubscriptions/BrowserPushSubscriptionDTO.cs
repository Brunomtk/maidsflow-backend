namespace Core.DTO.PushSubscriptions
{
    /// <summary>
    /// Payload padrão do browser para inscrição de Web Push.
    /// Formato compatível com a API do navegador (endpoint, expirationTime, keys).
    /// </summary>
    public class BrowserPushSubscriptionDTO
    {
        public string Endpoint { get; set; } = null!;
        public long? ExpirationTime { get; set; }
        public PushSubscriptionKeysDTO Keys { get; set; } = new();
        public string? UserAgent { get; set; }
    }
}
