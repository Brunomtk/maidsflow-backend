using System;

namespace Core.Models
{
    /// <summary>
    /// Stores report email deliveries for auditability and scheduled-job idempotency.
    /// </summary>
    public class CompanyReportEmailDispatch : BaseModel
    {
        public int CompanyId { get; set; }
        public string RecipientEmail { get; set; } = string.Empty;
        public DateTime PeriodStartDate { get; set; }
        public DateTime PeriodEndDate { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; }
        public string TriggeredBy { get; set; } = string.Empty;
        public string? DispatchKey { get; set; }

        public Company? Company { get; set; }
    }
}
