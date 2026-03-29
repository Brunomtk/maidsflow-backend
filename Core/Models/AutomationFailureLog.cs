using System;

namespace Core.Models
{
    public class AutomationFailureLog : BaseModel
    {
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }
        public string Source { get; set; } = "n8n";
        public string WorkflowKey { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public string? NodeName { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ErrorDetails { get; set; }
        public string? ExecutionId { get; set; }
        public int? AppointmentId { get; set; }
        public string? PayloadJson { get; set; }
        public string? AlertEmailTo { get; set; }
        public bool AlertEmailSent { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public DateTime? AlertEmailSentAtUtc { get; set; }
    }
}
