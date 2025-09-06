using Core.Enums;

namespace Core.DTO.Professional
{
    public class UpdateProfessionalRequest
    {
        public string? Name { get; set; }
        public string? Cpf { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int? TeamId { get; set; }
        public StatusEnum? Status { get; set; }
    }
}