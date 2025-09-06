using System.ComponentModel.DataAnnotations;

namespace Core.DTO.InternalFeedback
{
    public class CreateInternalFeedbackCommentDTO
    {
        public int AuthorId { get; set; }
        public string Author { get; set; }
        public string Text { get; set; }
    }
}
