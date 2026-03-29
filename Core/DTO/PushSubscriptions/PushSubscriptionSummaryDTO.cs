using System;

namespace Core.DTO.PushSubscriptions
{
    public class PushSubscriptionSummaryDTO
    {
        public int Id { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
        public string? BrowserName { get; set; }
        public bool IsPwaInstalled { get; set; }
        public string? PermissionState { get; set; }
        public bool IsActive { get; set; }
        public int FailureCount { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
        public DateTime? LastPushAttemptAtUtc { get; set; }
        public DateTime? LastSuccessfulPushAtUtc { get; set; }
        public DateTime? LastPushOpenedAtUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
