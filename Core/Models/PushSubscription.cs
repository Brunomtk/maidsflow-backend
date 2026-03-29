using System;

namespace Core.Models
{
    /// <summary>
    /// Assinatura Web Push (Push API) do navegador / PWA para um usuário.
    /// Salva endpoint + chaves (p256dh/auth) e metadados do device para tornar
    /// o envio mais confiável em produção.
    /// </summary>
    public class PushSubscription : BaseModel
    {
        public int UserId { get; set; }
        public int? CompanyId { get; set; }
        public string? UserRole { get; set; }

        public string Endpoint { get; set; } = null!;

        // Keys geradas pelo browser (PushSubscription.keys)
        public string P256dh { get; set; } = null!;
        public string Auth { get; set; } = null!;

        /// <summary>
        /// Alguns browsers podem informar expirationTime (ms epoch). Pode ser null.
        /// </summary>
        public long? ExpirationTime { get; set; }

        public string? UserAgent { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
        public string? BrowserName { get; set; }
        public bool IsPwaInstalled { get; set; }
        public string? PermissionState { get; set; }
        public bool IsActive { get; set; } = true;
        public int FailureCount { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
        public DateTime? LastPushAttemptAtUtc { get; set; }
        public DateTime? LastSuccessfulPushAtUtc { get; set; }
        public DateTime? LastPushOpenedAtUtc { get; set; }
        public string? LastError { get; set; }
    }
}
