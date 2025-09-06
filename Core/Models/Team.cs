using Core.Enum;
using Core.Enums;

namespace Core.Models
{
    public class Team : BaseModel
    {
        public string Name { get; set; }
        public string Region { get; set; }
        public string Description { get; set; }
        public decimal Rating { get; set; }
        public int CompletedServices { get; set; }
        public StatusEnum Status { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }  // nullable

        public int? LeaderId { get; set; }
        public Leader? Leader { get; set; }    // nullable
    }
}
