using System;

namespace Core.Models
{
    public class InternalFeedbackComment : BaseModel
    {
        public int Id { get; set; }
        public int InternalFeedbackId { get; set; }  // FK back to InternalFeedback
        public int AuthorId { get; set; }
        public string Author { get; set; } = null!;
        public DateTime Date { get; set; }
        public string Text { get; set; } = null!;
    }
}
