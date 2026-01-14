using Core.Enums;
using System.Numerics;

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


        // Agora é opcional (pode ser definido depois, via update ou via assinatura).
        public int? PlanId { get; set; }

        public StatusEnum Status { get; set; } = StatusEnum.Active;

        public Plan? Plan { get; set; }

        public ICollection<User>? Users { get; set; }
    }
}
