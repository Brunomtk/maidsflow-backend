using System;
using Core.Enums.BackgroundJobs;

namespace Core.DTO.BackgroundJobs
{
    public class BackgroundJobStatusDTO
    {
        public string JobKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public bool IsEnabled { get; set; }
        public BackgroundJobRunStatus CurrentStatus { get; set; }
        public DateTime? LastStartedAtUtc { get; set; }
        public DateTime? LastFinishedAtUtc { get; set; }
        public DateTime? LastSuccessAtUtc { get; set; }
        public DateTime? LastFailureAtUtc { get; set; }
        public DateTime? LastHeartbeatAtUtc { get; set; }
        public long? LastDurationMs { get; set; }
        public string? LastError { get; set; }
        public string? LastSummary { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? NextPlannedRunAtUtc { get; set; }
    }
}
