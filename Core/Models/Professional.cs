using System;
using Core.Enums;

namespace Core.Models
{
    public class Professional
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public int? TeamId { get; set; }
        public int CompanyId { get; set; }

        public StatusEnum Status { get; set; }

        public double? Rating { get; set; }
        public int? CompletedServices { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }

        // ✅ Propriedades de navegação
        public Company Company { get; set; } = null!;
        public Team? Team { get; set; }
    }
}
