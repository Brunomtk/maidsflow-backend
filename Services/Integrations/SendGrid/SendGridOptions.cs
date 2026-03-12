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

    /// <summary>Default subject used for plan payment failed emails.</summary>
    public string PlanPaymentFailedSubject { get; set; } = "Payment failed";
    public string PasswordResetSubject { get; set; } = "Reset your MaidsFlow password";

    /// <summary>
    /// Base URL of the public review form on the FRONT-END.
    /// Example: https://app.maidsflow.com/review
    /// The token will be appended as: {baseUrl}/{token}
    /// </summary>
    public string PublicReviewFormBaseUrl { get; set; } = string.Empty;

    /// <summary>Default subject used for review request emails.</summary>
    public string ReviewRequestSubject { get; set; } = "How was your service?";
    /// <summary>Disable SendGrid click tracking so links open directly.</summary>
    public bool DisableClickTracking { get; set; } = true;

}
