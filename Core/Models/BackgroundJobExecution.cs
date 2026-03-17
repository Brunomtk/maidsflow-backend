using System;
using Core.Enums.BackgroundJobs;

namespace Core.Models
{
    public class BackgroundJobExecution : BaseModel
    {
        public required string JobKey { get; set; }
        public BackgroundJobRunStatus Status { get; set; } = BackgroundJobRunStatus.Idle;
        public DateTime StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public long? DurationMs { get; set; }
        public string? Summary { get; set; }
        public string? Error { get; set; }
        public int? ItemsProcessed { get; set; }
        public int? ItemsSucceeded { get; set; }
        public int? ItemsFailed { get; set; }
        public string TriggeredBy { get; set; } = "system";
    }
}
