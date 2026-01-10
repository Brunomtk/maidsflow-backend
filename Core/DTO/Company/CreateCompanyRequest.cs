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

        // Notification preferences (default: enabled)
        public bool ReceiveSms { get; set; } = true;
        public bool ReceiveEmail { get; set; } = true;
        // Agora é opcional (pode ser atribuído depois).
        public int? PlanId { get; set; }
        public StatusEnum Status { get; set; } = StatusEnum.Active;
    }
}
