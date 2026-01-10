using System;
using System.Collections.Generic;
using Core.Enums.Payroll;

namespace Core.Models
{
    public class PayrollRun
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ClosedDate { get; set; }
        public DateTime? PaidDate { get; set; }

        public List<PayrollItem> Items { get; set; } = new();
    }
}
