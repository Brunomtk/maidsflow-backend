using System.Collections.Generic;
using Core.Enums;

namespace Core.DTO.Plan
{
    public class UpdatePlanRequest
    {
        public required string Name { get; set; }
        public required decimal Price { get; set; }
        public required List<string> Features { get; set; } = new();
        public required int Duration { get; set; }
        public required StatusEnum Status { get; set; }

        public LimitsDTO Limits { get; set; } = new();
    }
}
