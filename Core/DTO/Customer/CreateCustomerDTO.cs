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

        [Phone]
        public string? Phone { get; set; }

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

        [Required]
        public int CompanyId { get; set; }

        public ClientType? ClientType { get; set; }
        public StatusEnum? Status { get; set; }
    }
}