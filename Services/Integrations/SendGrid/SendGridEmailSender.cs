using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Services.Integrations.SendGrid;

public class SendGridEmailSender : ISendGridEmailSender
{
    private readonly HttpClient _http;
    private readonly SendGridOptions _opt;

    public SendGridEmailSender(HttpClient http, IOptions<SendGridOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public async Task<SendGridSendResult> SendAsync(SendGridEmailMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.ApiKey))
            return new SendGridSendResult(false, 0, Error: "SendGrid ApiKey is not configured.");

        if (string.IsNullOrWhiteSpace(_opt.FromEmail))
            return new SendGridSendResult(false, 0, Error: "SendGrid FromEmail is not configured.");

        var baseUrl = string.IsNullOrWhiteSpace(_opt.ApiBaseUrl)
            ? "https://api.sendgrid.com"
            : _opt.ApiBaseUrl.Trim().TrimEnd('/');

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);

        var payload = new SendGridMailSendRequest
        {
            From = new SendGridEmailAddress { Email = _opt.FromEmail, Name = _opt.FromName },
            Personalizations = new[]
            {
                new SendGridPersonalization
                {
                    To = new[]
                    {
                        new SendGridEmailAddress { Email = message.ToEmail, Name = message.ToName }
                    }
                }
            },
            Subject = message.Subject,
            Content = new[]
            {
                new SendGridContent { Type = "text/plain", Value = message.PlainText },
                new SendGridContent { Type = "text/html", Value = message.Html }
            },
            TrackingSettings = _opt.DisableClickTracking
                ? new SendGridTrackingSettings
                {
                    ClickTracking = new SendGridClickTracking
                    {
                        Enable = false,
                        EnableText = false
                    }
                }
                : null
        };

        request.Content = JsonContent.Create(payload);

        try
        {
            var resp = await _http.SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new SendGridSendResult(resp.IsSuccessStatusCode, (int)resp.StatusCode, body,
                resp.IsSuccessStatusCode ? null : "SendGrid request failed");
        }
        catch (Exception ex)
        {
            return new SendGridSendResult(false, 0, Error: ex.Message);
        }
    }

    private sealed class SendGridMailSendRequest
    {
        [JsonPropertyName("personalizations")] public SendGridPersonalization[] Personalizations { get; set; } = Array.Empty<SendGridPersonalization>();
        [JsonPropertyName("from")] public SendGridEmailAddress From { get; set; } = new();
        [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
        [JsonPropertyName("content")] public SendGridContent[] Content { get; set; } = Array.Empty<SendGridContent>();
        [JsonPropertyName("tracking_settings")] public SendGridTrackingSettings? TrackingSettings { get; set; }
    }

    private sealed class SendGridTrackingSettings
    {
        [JsonPropertyName("click_tracking")] public SendGridClickTracking? ClickTracking { get; set; }
    }

    private sealed class SendGridClickTracking
    {
        [JsonPropertyName("enable")] public bool Enable { get; set; }
        [JsonPropertyName("enable_text")] public bool EnableText { get; set; }
    }

    private sealed class SendGridPersonalization
    {
        [JsonPropertyName("to")] public SendGridEmailAddress[] To { get; set; } = Array.Empty<SendGridEmailAddress>();
    }

    private sealed class SendGridEmailAddress
    {
        [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class SendGridContent
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "text/plain";
        [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
    }
}
