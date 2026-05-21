using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Customer
{
    public class UpdateCustomerDTO
    {
        [Required]
        public int Id { get; set; }

        public string? Name { get; set; }
        public string? Ssn { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }

        public string? Phone2 { get; set; }

        public bool? ReceiveSms { get; set; }
        public bool? ReceiveEmail { get; set; }

        /// <summary>Preferred language ("en", "pt-BR", "es", "fr"). Optional.</summary>
        [MaxLength(10)]
        public string? Language { get; set; }

        public string? Address { get; set; }
        public string? ZipCode { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Observations { get; set; }

        public decimal? Ticket { get; set; }
        public string? Frequency { get; set; }
        public string? PaymentMethod { get; set; }

        public ClientType? ClientType { get; set; }
        public StatusEnum? Status { get; set; }
    }
}
