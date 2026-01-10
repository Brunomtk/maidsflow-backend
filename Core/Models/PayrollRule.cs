using System;
using System.ComponentModel.DataAnnotations;
using Core.Enums.Payroll;
using Core.Enums.Team;

namespace Core.Models
{
    public class PayrollRule
    {
        [Key]
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>
        /// Se ServiceTypeId for null, a regra é geral (vale para qualquer ServiceType).
        /// </summary>
        public int? ServiceTypeId { get; set; }
        public ServiceType? ServiceType { get; set; }

        public TeamMemberRole TeamRole { get; set; } = TeamMemberRole.Member;

        public RateType RateType { get; set; } = RateType.Fixed;

        /// <summary>
        /// Se Fixed: valor em dinheiro. Se Percent: percentual (ex.: 40 = 40%).
        /// </summary>
        public decimal RateValue { get; set; }

        /// <summary>
        /// Usado para desempate (maior prioridade vence).
        /// </summary>
        public int Priority { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
