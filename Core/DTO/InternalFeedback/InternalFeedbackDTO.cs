// Core/DTO/InternalFeedback/InternalFeedbackDTO.cs
using System;
using System.Collections.Generic;
using Core.Enums;
using Core.Enums.InternalFeedback;

namespace Core.DTO.InternalFeedback
{
    public class InternalFeedbackDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ProfessionalId { get; set; }
        public string Professional { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string Team { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public InternalFeedbackStatus Status { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public InternalFeedbackPriority Priority { get; set; }
        public int AssignedToId { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public List<InternalFeedbackCommentDTO> Comments { get; set; } = new();
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }

    public class InternalFeedbackCommentDTO
    {
        public int Id { get; set; }
        public int AuthorId { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}
