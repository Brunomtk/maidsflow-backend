using Core.Enums;

namespace Core.DTO.Plan
{
    public class PlanDTO
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal Price { get; set; }
        public List<string> Features { get; set; } = new();
        public int Duration { get; set; }
        public StatusEnum Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public LimitsDTO Limits { get; set; } = new();
    }
}
