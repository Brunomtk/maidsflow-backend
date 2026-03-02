namespace Core.DTO.User
{
    public class GoogleAuthenticateRequest
    {
        public string IdToken { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = true;

        /// <summary>
        /// Optional override for the name to store when creating a new user.
        /// </summary>
        public string? Name { get; set; }
    }
}
