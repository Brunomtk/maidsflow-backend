using System;

namespace Core.DTO.Teams
{
    public class TeamDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? LeaderId { get; set; }
        public string? Region { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "active";
        public string CompanyId { get; set; } = string.Empty;
        public double? Rating { get; set; }
        public int? CompletedServices { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}