using Core.Enums.InternalFeedback;
using System;
using System.Collections.Generic;

namespace Core.Models
{
    public class InternalFeedback : BaseModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public int ProfessionalId { get; set; }
        public int TeamId { get; set; }
        public string Category { get; set; } = null!;
        public InternalFeedbackStatus Status { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public InternalFeedbackPriority Priority { get; set; }
        public int AssignedToId { get; set; }

        // Navigation
        public List<InternalFeedbackComment> Comments { get; set; } = new();
    }
}
