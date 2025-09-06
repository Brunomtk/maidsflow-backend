// Core/DTO/InternalFeedback/InternalFeedbackFiltersDTO.cs
using Core.Enums;
using Core.Enums.InternalFeedback;

namespace Core.DTO.InternalFeedback
{
    public class InternalFeedbackFiltersDTO
    {
        public string Status { get; set; } = "all";
        public string Priority { get; set; } = "all";
        public string? Category { get; set; }
        public int? ProfessionalId { get; set; }
        public int? TeamId { get; set; }
        public string? Search { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
