using System;

namespace Core.DTO.AutomationAlerts
{
    public class AutomationFailureLogDto
    {
        public int Id { get; set; }
        public int? CompanyId { get; set; }
        public string Source { get; set; } = string.Empty;
        public string WorkflowKey { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public string? NodeName { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ErrorDetails { get; set; }
        public string? ExecutionId { get; set; }
        public int? AppointmentId { get; set; }
        public string? AlertEmailTo { get; set; }
        public bool AlertEmailSent { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public DateTime? AlertEmailSentAtUtc { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
