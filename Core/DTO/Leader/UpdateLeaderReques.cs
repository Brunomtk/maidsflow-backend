namespace Core.DTO.Leader
{
    public class UpdateLeaderRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Status { get; set; } // será convertido para enum no controller
    }
}
