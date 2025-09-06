// Core/DTO/InternalFeedback/UpdateInternalFeedbackDTO.cs
using System;
using Core.Enums;
using Core.Enums.InternalFeedback;

namespace Core.DTO.InternalFeedback
{
    public class UpdateInternalFeedbackDTO
    {
        public string? Title { get; set; }
        public int? ProfessionalId { get; set; }
        public int? TeamId { get; set; }
        public string? Category { get; set; }
        public InternalFeedbackStatus? Status { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public InternalFeedbackPriority? Priority { get; set; }
        public int? AssignedToId { get; set; }
    }
}
