namespace Core.DTO.PushSubscriptions
{
    /// <summary>
    /// Payload padrão do browser/PWA para inscrição de Web Push.
    /// Mantém compatibilidade com endpoint/expirationTime/keys e adiciona metadados
    /// para termos controle confiável por device.
    /// </summary>
    public class BrowserPushSubscriptionDTO
    {
        public string Endpoint { get; set; } = null!;
        public long? ExpirationTime { get; set; }
        public PushSubscriptionKeysDTO Keys { get; set; } = new();
        public string? UserAgent { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
        public string? BrowserName { get; set; }
        public bool? IsPwaInstalled { get; set; }
        public string? PermissionState { get; set; }
    }
}
