using Core.Enums;

namespace Core.DTO.Company
{
    public class CreateCompanyRequest
    {
        public required string Name { get; set; }
        public required string Cnpj { get; set; }
        public required string Responsible { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }
        public required int PlanId { get; set; }
        public StatusEnum Status { get; set; } = StatusEnum.Active;
    }
}
