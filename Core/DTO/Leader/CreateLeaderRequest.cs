public class CreateLeaderRequest
{
    public int UserId { get; set; } // necessário
    public string Region { get; set; } = string.Empty; // necessário
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
