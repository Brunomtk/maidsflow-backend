using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.Models
{
    public class Customer : BaseModel
    {
        public ClientType ClientType { get; set; } = ClientType.Normal;

        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Optional second phone number. The system supports a maximum of 2 phone numbers per customer:
        /// Phone and Phone2.
        /// </summary>
        public string? Phone2 { get; set; }

        public string Address { get; set; } = string.Empty;
        public string? ZipCode { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Observations { get; set; }

        // Notification preferences (default: enabled)
        public bool ReceiveSms { get; set; } = true;
        public bool ReceiveEmail { get; set; } = true;

        /// <summary>
        /// Preferred language for outbound communication (SMS, email).
        /// Format: BCP-47-ish ("en", "pt-BR", "es", "fr"). Null falls back to Company.Language, then "en".
        /// </summary>
        [MaxLength(10)]
        public string? Language { get; set; }

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

        // Navegação: lista de agendamentos vinculados a este cliente.
        // Observação: os appointments já possuem CustomerId (nullable), então aqui é apenas navegação.
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();

        // Navegação: lista de pagamentos vinculados a este cliente (opcional)
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}