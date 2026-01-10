namespace Core.DTO.Payroll
{
    public class PayrollPreviewProfessionalSummaryDTO
    {
        public int ProfessionalId { get; set; }
        public string? ProfessionalName { get; set; }
        public int AppointmentsCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int MissingRulesCount { get; set; }
    }
}
