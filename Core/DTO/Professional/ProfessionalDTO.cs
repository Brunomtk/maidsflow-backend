using System;

namespace Core.DTO.Professional
{
    public class ProfessionalDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public int? TeamId { get; set; }
        public int CompanyId { get; set; }

        public string Status { get; set; } = string.Empty;

        public double? Rating { get; set; }
        public int? CompletedServices { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}