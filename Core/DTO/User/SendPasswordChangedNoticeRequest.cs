namespace Core.DTO.User;

public class SendPasswordChangedNoticeRequest
{
    /// <summary>
    /// Optional login URL to show in the email.
    /// If not provided, the backend will use SendGrid:SupportUrl.
    /// </summary>
    public string? LoginUrl { get; set; }
}

public class SendPasswordChangedNoticeResponse
{
    public int UserId { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public bool EmailSent { get; set; }
    public int? ProviderStatusCode { get; set; }
    public string? ProviderResponse { get; set; }
}
