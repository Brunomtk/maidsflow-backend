using Core.Enums;
using System.Numerics;

namespace Core.Models
{
    public class Company : BaseModel
    {
        public required string Name { get; set; }
        public required string Cnpj { get; set; }
        public required string Responsible { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }

        public required int PlanId { get; set; }

        public StatusEnum Status { get; set; } = StatusEnum.Active;

        public Plan? Plan { get; set; }

        public ICollection<User>? Users { get; set; }
    }
}
