using System.Collections.Generic;

namespace Core.Models
{
    public class PaymentCategory : BaseModel
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = null!;
        public bool IsSystem { get; set; }
        public bool Active { get; set; } = true;

        public Company? Company { get; set; }
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
