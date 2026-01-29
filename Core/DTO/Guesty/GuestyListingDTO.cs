namespace Core.DTO.Guesty
{
    public class GuestyListingDTO
    {
        public string Id { get; set; } = string.Empty;
        public string? Nickname { get; set; }
        public string? Title { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PictureUrl { get; set; }
        public string? Status { get; set; }
    }
}
