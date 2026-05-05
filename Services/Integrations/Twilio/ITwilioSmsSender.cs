namespace Services.Integrations.Twilio;

public interface ITwilioSmsSender
{
    /// <summary>Send using the default Twilio:FromNumber configured in appsettings.</summary>
    Task<(string messageSid, string rawResponse)> SendSmsAsync(string to, string body, CancellationToken ct = default);

    /// <summary>Send using an explicit FROM phone (E.164) — e.g. company-owned Twilio number.</summary>
    Task<(string messageSid, string rawResponse)> SendSmsFromAsync(string from, string to, string body, CancellationToken ct = default);
}
