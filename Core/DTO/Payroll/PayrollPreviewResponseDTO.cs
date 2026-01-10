using System;
using System.Collections.Generic;

namespace Core.DTO.Payroll
{
    public class PayrollPreviewResponseDTO
    {
        public int CompanyId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public List<PayrollPreviewItemDTO> Items { get; set; } = new();
        public List<PayrollPreviewProfessionalSummaryDTO> Summaries { get; set; } = new();

        public decimal TotalAmount { get; set; }
        public int TotalItems { get; set; }
        public int TotalMissingRules { get; set; }
    }
}
