using Core.Enums;
using System;
using System.Collections.Generic;

namespace Core.Models
{
    public class Plan : BaseModel
    {
        public required string Name { get; set; }
        public decimal Price { get; set; }

        public List<string> Features { get; set; } = new();

        public int? ProfessionalsLimit { get; set; }
        public int? TeamsLimit { get; set; }
        public int? CustomersLimit { get; set; }
        public int? AppointmentsLimit { get; set; }

        /// <summary>
        /// Duração do plano em meses
        /// </summary>
        public int Duration { get; set; }

        public StatusEnum Status { get; set; } = StatusEnum.Active;

        public ICollection<PlanSubscription>? Subscriptions { get; set; }
    }
}
