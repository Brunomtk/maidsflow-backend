namespace Services.Integrations.SendGrid;

public class SendGridOptions
{
    /// <summary>SendGrid API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for SendGrid API.
    /// Use https://api.sendgrid.com (US) or https://api.eu.sendgrid.com (EU).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.sendgrid.com";

    /// <summary>Sender email.</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Sender display name.</summary>
    public string FromName { get; set; } = "MaidsFlow";

    /// <summary>Support URL shown in email footer/button (optional).</summary>
    public string SupportUrl { get; set; } = "https://maidsflow.com";

    /// <summary>Default subject used for credentials emails.</summary>
    public string CredentialsSubject { get; set; } = "Your MaidsFlow access credentials";

    /// <summary>Default subject used for password changed notice emails.</summary>
    public string PasswordChangedSubject { get; set; } = "Your MaidsFlow password was changed";


    /// <summary>Default subject used for plan payment success emails.</summary>
    public string PlanPaymentSuccessSubject { get; set; } = "Payment successful";
}
