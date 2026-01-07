namespace Services.Integrations.Twilio;

public interface ITwilioSmsSender
{
    Task<(string messageSid, string rawResponse)> SendSmsAsync(string to, string body, CancellationToken ct = default);
}
