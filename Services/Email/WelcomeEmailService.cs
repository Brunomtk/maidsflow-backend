using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;

namespace Services.Email;

public sealed class WelcomeEmailService : IWelcomeEmailService
{
    private readonly ISendGridEmailSender _sender;
    private readonly SendGridOptions _opt;

    public WelcomeEmailService(ISendGridEmailSender sender, IOptions<SendGridOptions> opt)
    {
        _sender = sender;
        _opt = opt.Value;
    }

    public async Task SendWelcomeAsync(string toEmail, string? toName, string? loginUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        var model = new WelcomeEmailTemplate.Model(
            Name: string.IsNullOrWhiteSpace(toName) ? "there" : toName!,
            Email: toEmail,
            LoginUrl: loginUrl,
            SupportUrl: _opt.SupportUrl
        );

        var (html, plain) = WelcomeEmailTemplate.Render(model);

        var subject = string.IsNullOrWhiteSpace(_opt.WelcomeSubject)
            ? "Welcome to MaidsFlow"
            : _opt.WelcomeSubject.Trim();

        var msg = new SendGridEmailMessage(
            ToEmail: toEmail,
            Subject: subject,
            PlainText: plain,
            Html: html,
            ToName: toName
        );

        await _sender.SendAsync(msg, ct);
    }
}
