using System;

namespace Core.Models
{
    /// <summary>
    /// Assinatura Web Push (Push API) do navegador / PWA para um usuário.
    /// Salva endpoint + chaves (p256dh/auth) necessárias para enviar push.
    /// </summary>
    public class PushSubscription : BaseModel
    {
        public int UserId { get; set; }

        public string Endpoint { get; set; } = null!;

        // Keys geradas pelo browser (PushSubscription.keys)
        public string P256dh { get; set; } = null!;
        public string Auth { get; set; } = null!;

        /// <summary>
        /// Alguns browsers podem informar expirationTime (ms epoch). Pode ser null.
        /// </summary>
        public long? ExpirationTime { get; set; }

        public string? UserAgent { get; set; }
    }
}
