using System;
using System.Collections.Generic;

namespace Core.Models
{
    public class CustomerAddress : BaseModel
    {
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public string? Label { get; set; }

        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }

        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? ZipCode { get; set; }

        public string? Observations { get; set; }

        public decimal? Ticket { get; set; }
        public string? Frequency { get; set; }
        public string? PaymentMethod { get; set; }

        public bool IsPrimary { get; set; }

        public ICollection<CustomerArea> Areas { get; set; } = new List<CustomerArea>();
        public ICollection<Checklist> Checklists { get; set; } = new List<Checklist>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
