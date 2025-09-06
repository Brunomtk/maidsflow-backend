namespace Core.DTO.Leader
{
    public class LeaderDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedDate { get; set; }  
    }
}
