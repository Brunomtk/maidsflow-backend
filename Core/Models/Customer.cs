using System;
using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.Models
{
    public class Customer : BaseModel
    {
        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Observations { get; set; }

        [MaxLength(11)]
        public string? Ssn { get; set; }

        public decimal? Ticket { get; set; }

        [MaxLength(50)]
        public string? Frequency { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        public StatusEnum Status { get; set; } = StatusEnum.Active;

        public int CompanyId { get; set; } 
        public Company Company { get; set; } = null!;
    }
}
