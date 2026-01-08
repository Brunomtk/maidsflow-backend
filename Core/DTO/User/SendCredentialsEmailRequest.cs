namespace Core.DTO.User;

public class SendCredentialsEmailRequest
{
    /// <summary>
    /// If true, generates a new random password, updates the user and marks Onboarding=true.
    /// </summary>
    public bool GenerateNewPassword { get; set; } = true;

    /// <summary>
    /// Optional login URL to show in the email.
    /// If not provided, the backend will use SendGrid:SupportUrl.
    /// </summary>
    public string? LoginUrl { get; set; }
}

public class SendCredentialsEmailResponse
{
    public int UserId { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public bool PasswordRegenerated { get; set; }
    public string? GeneratedPassword { get; set; }
    public bool EmailSent { get; set; }
    public int? ProviderStatusCode { get; set; }
    public string? ProviderResponse { get; set; }
}