using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Services.Integrations.Twilio;

public class TwilioSmsSender : ITwilioSmsSender
{
    private readonly HttpClient _http;
    private readonly TwilioOptions _opt;

    public TwilioSmsSender(HttpClient http, IOptions<TwilioOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public async Task<(string messageSid, string rawResponse)> SendSmsAsync(string to, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccountSid) ||
            string.IsNullOrWhiteSpace(_opt.AuthToken) ||
            string.IsNullOrWhiteSpace(_opt.FromNumber))
            throw new InvalidOperationException("Twilio não configurado (Twilio:AccountSid/AuthToken/FromNumber).");

        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Número de destino (To) é obrigatório.", nameof(to));

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_opt.AccountSid}/Messages.json";

        // Basic Auth: AccountSid:AuthToken
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_opt.AccountSid}:{_opt.AuthToken}"));

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = _opt.FromNumber,
            ["Body"] = body ?? string.Empty
        });

        var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"Twilio error {(int)res.StatusCode}: {raw}");

        // Response JSON has "sid"
        string sid = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("sid", out var sidEl))
                sid = sidEl.GetString() ?? string.Empty;
        }
        catch
        {
            // ignore parse failures, still return raw
        }

        return (sid, raw);
    }
}
