namespace Core.DTO.User
{
    public class TokenJWT
    {
        public required string Token { get; set; }
        public string? RefreshToken { get; set; }
    }
}
