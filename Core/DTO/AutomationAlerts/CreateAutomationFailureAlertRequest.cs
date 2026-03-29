using System;
using System.Text.Json.Serialization;

namespace Core.DTO.AutomationAlerts
{
    public class CreateAutomationFailureAlertRequest
    {
        public int? CompanyId { get; set; }
        public string? Source { get; set; }
        public string? WorkflowKey { get; set; }
        public string? WorkflowName { get; set; }
        public string? NodeName { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorDetails { get; set; }
        public string? ExecutionId { get; set; }
        public int? AppointmentId { get; set; }
        public string? PayloadJson { get; set; }
        public string? AlertEmailTo { get; set; }
        public DateTime? OccurredAtUtc { get; set; }

        [JsonPropertyName("secret")]
        public string? Secret { get; set; }
    }
}
