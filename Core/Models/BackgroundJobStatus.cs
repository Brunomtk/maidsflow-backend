using System;
using Core.Enums.BackgroundJobs;

namespace Core.Models
{
    public class BackgroundJobStatus : BaseModel
    {
        public required string JobKey { get; set; }
        public required string DisplayName { get; set; }
        public string? Category { get; set; }
        public bool IsEnabled { get; set; } = true;
        public BackgroundJobRunStatus CurrentStatus { get; set; } = BackgroundJobRunStatus.Idle;
        public DateTime? LastStartedAtUtc { get; set; }
        public DateTime? LastFinishedAtUtc { get; set; }
        public DateTime? LastSuccessAtUtc { get; set; }
        public DateTime? LastFailureAtUtc { get; set; }
        public DateTime? LastHeartbeatAtUtc { get; set; }
        public long? LastDurationMs { get; set; }
        public string? LastError { get; set; }
        public string? LastSummary { get; set; }
        public int ConsecutiveFailures { get; set; } = 0;
        public DateTime? NextPlannedRunAtUtc { get; set; }
    }
}
