using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Customer
{
    /// <summary>
    /// Uma linha (row) da importação em lote de clientes.
    /// Campos e nomes foram pensados para bater com a planilha (Excel template).
    /// </summary>
    public class BulkCreateCustomerRowDTO
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [MaxLength(11)]
        public string? Ssn { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? Phone { get; set; }

        [Phone]
        public string? Phone2 { get; set; }

        public string? ZipCode { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Observations { get; set; }

        public decimal? Ticket { get; set; }

        [MaxLength(50)]
        public string? Frequency { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        /// <summary>
        /// Se não informado, assume true.
        /// </summary>
        public bool? ReceiveSms { get; set; }

        /// <summary>
        /// Se não informado, assume true.
        /// </summary>
        public bool? ReceiveEmail { get; set; }

        /// <summary>
        /// Idioma preferido do cliente para mensagens automáticas ("en", "pt-BR", "es", "fr"). Opcional.
        /// </summary>
        [MaxLength(10)]
        public string? Language { get; set; }
    }
}
