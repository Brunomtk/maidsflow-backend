using Core.Enums;

namespace Core.DTO.Company
{
    public class CompanyDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Responsible { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public int PlanId { get; set; }            // ID do plano associado
        public string? PlanName { get; set; }      // Nome do plano, opcional

        public StatusEnum Status { get; set; }     // Status da empresa (Active/Inactive/etc.)
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
