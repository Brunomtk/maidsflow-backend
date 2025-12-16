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
        public string Address { get; set; } = string.Empty;
        public string? ZipCode { get; set; }
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

        // Navegação: lista de agendamentos vinculados a este cliente.
        // Observação: os appointments já possuem CustomerId (nullable), então aqui é apenas navegação.
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}