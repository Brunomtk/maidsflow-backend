using System;

namespace Core.DTO.Review
{
    public class ReviewLinkDTO
    {
        public int ReviewId { get; set; }
        public Guid Token { get; set; }
        public string? Url { get; set; }
    }
}
