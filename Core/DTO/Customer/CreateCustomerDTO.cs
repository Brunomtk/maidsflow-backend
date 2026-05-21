using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Customer
{
    public class CreateCustomerDTO
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [MaxLength(11)]
        public string? Ssn { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Phone2 { get; set; }

        [Required]
        public string Address { get; set; } = string.Empty;

        public string? ZipCode { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Observations { get; set; }

        public decimal? Ticket { get; set; }

        [MaxLength(50)]
        public string? Frequency { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        // Notification preferences (default: enabled)
        public bool ReceiveSms { get; set; } = true;
        public bool ReceiveEmail { get; set; } = true;

        /// <summary>Preferred language ("en", "pt-BR", "es", "fr"). Optional.</summary>
        [MaxLength(10)]
        public string? Language { get; set; }

        [Required]
        public int CompanyId { get; set; }

        public ClientType? ClientType { get; set; }
        public StatusEnum? Status { get; set; }
    }
}