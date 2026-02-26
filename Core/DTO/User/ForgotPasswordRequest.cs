using System.ComponentModel.DataAnnotations;

namespace Core.DTO.User;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the web app (ex: "https://app.maidsflow.com.br").
    /// If provided, the reset link will be built as {BaseUrl}/reset-password?token=...
    /// </summary>
    public string? WebBaseUrl { get; set; }
}
