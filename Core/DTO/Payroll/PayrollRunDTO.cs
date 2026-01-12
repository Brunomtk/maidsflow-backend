using System;
using Core.Enums.Payroll;

namespace Core.DTO.Payroll
{
    public class PayrollRunDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public PayrollRunStatus Status { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public DateTime? PaidDate { get; set; }

        public int ItemsCount { get; set; }
        public int MissingRulesCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
