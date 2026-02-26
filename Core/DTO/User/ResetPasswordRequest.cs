using System.ComponentModel.DataAnnotations;

namespace Core.DTO.User;

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;

    public string? LoginUrl { get; set; }
}
