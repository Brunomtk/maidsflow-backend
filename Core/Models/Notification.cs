using System;
using Core.Enums.Notifications;
using Core.Enums.User;

namespace Core.Models
{
    public class Notification : BaseModel
    {
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;

        public NotificationType Type { get; set; }

        // Who should receive it
        public int RecipientId { get; set; }
        public UserRole RecipientRole { get; set; }

        // Optional: if sent on behalf of a company
        public int? CompanyId { get; set; }

        public NotificationStatus Status { get; set; }

        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
