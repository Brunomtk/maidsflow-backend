using System.Collections.Generic;

namespace Core.DTO.Payroll
{
    public class PayrollRunDetailsDTO
    {
        public PayrollRunDTO Run { get; set; } = new();
        public List<PayrollItemDTO> Items { get; set; } = new();
        public List<PayrollPreviewProfessionalSummaryDTO> Summaries { get; set; } = new();
    }
}
