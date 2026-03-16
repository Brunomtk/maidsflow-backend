using Core.Enums;
using System;
using System.Collections.Generic;

namespace Core.Models
{
    public class Company : BaseModel
    {
        public required string Name { get; set; }
        public required string Cnpj { get; set; }
        public required string Responsible { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }

        // Notification preferences (default: enabled)
        public bool ReceiveSms { get; set; } = true;
        public bool ReceiveEmail { get; set; } = true;

        // Optional S3 key for company avatar image (stored in S3; presigned URLs are generated on demand).
        public string? AvatarKey { get; set; }

        // Stripe customer id (used for billing / subscriptions)
        public string? StripeCustomerId { get; set; }

        // -------------------- Guesty Integration --------------------
        // Booking Engine API or Open API authentication.
        // If you don't have DB encryption, consider moving secrets to a secret manager.

        public string? GuestyAccessToken { get; set; }
        public string? GuestyTokenType { get; set; }
        public DateTime? GuestyTokenExpiresAtUtc { get; set; }
        public DateTime? GuestyTokenUpdatedAtUtc { get; set; }

        public string? GuestyClientId { get; set; }
        public string? GuestyClientSecret { get; set; }

        // "bookingEngine" (default) or "openApi"
        public string? GuestyApiType { get; set; }

        // Optional override for OAuth base URL (e.g. https://booking.guesty.com or https://open-api.guesty.com)
        public string? GuestyAuthBaseUrl { get; set; }

        // Optional override for oauth scope (default depends on GuestyApiType)
        public string? GuestyAuthScope { get; set; }

        // Agora é opcional (pode ser definido depois, via update ou via assinatura).
        public int? PlanId { get; set; }

        public StatusEnum Status { get; set; } = StatusEnum.Active;

        // Tracks whether the company has completed the initial onboarding/setup wizard.
        public bool HasCompletedInitialSetup { get; set; } = false;

        public Plan? Plan { get; set; }

        public ICollection<User>? Users { get; set; }
    }
}
