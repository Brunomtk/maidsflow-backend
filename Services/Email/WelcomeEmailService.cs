using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;

namespace Services.Email;

/// <summary>
/// Sends a "Welcome / Account created" email in English, matching the existing SendGrid styling.
/// This is intended to be used automatically right after user creation.
/// </summary>
public sealed class WelcomeEmailService : IWelcomeEmailService
{
    private readonly IUnitOfWork _uow;
    private readonly ISendGridEmailSender _emailSender;
    private readonly SendGridOptions _options;

    public WelcomeEmailService(
        IUnitOfWork uow,
        ISendGridEmailSender emailSender,
        IOptions<SendGridOptions> options)
    {
        _uow = uow;
        _emailSender = emailSender;
        _options = options.Value;
    }

    public async Task<SendWelcomeEmailResult> SendWelcomeEmailAsync(
        int userId,
        string? loginUrl,
        CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdWithPermissions(userId);
        if (user == null)
            throw new KeyNotFoundException("Usuário não encontrado.");

        var companyName = await ResolveCompanyNameAsync(user);
        var url = string.IsNullOrWhiteSpace(loginUrl) ? _options.SupportUrl : loginUrl;
        var subject = string.IsNullOrWhiteSpace(_options.WelcomeSubject)
            ? "Welcome to MaidsFlow"
            : _options.WelcomeSubject;

        var rendered = WelcomeEmailTemplate.Render(
            new WelcomeEmailTemplate.Payload(
                CompanyName: companyName,
                UserName: user.Name ?? string.Empty,
                Email: user.Email ?? string.Empty,
                Role: string.IsNullOrWhiteSpace(user.Role) ? "user" : user.Role,
                LoginUrl: url,
                CreatedAtUtc: user.CreatedDate == default ? DateTime.UtcNow : user.CreatedDate
            ),
            subject);

        var send = await _emailSender.SendAsync(new SendGridEmailMessage(
            ToEmail: user.Email ?? string.Empty,
            Subject: rendered.Subject,
            PlainText: rendered.PlainText,
            Html: rendered.Html,
            ToName: user.Name
        ), ct);

        return new SendWelcomeEmailResult(
            UserId: user.Id,
            ToEmail: user.Email ?? string.Empty,
            EmailSent: send.Ok,
            ProviderStatusCode: send.StatusCode == 0 ? null : send.StatusCode,
            ProviderResponse: send.ResponseBody ?? send.Error
        );
    }

    private async Task<string> ResolveCompanyNameAsync(Core.Models.User user)
    {
        if (user.CompanyId.HasValue)
        {
            var company = await _uow.Companies.GetByIdAsync(user.CompanyId.Value);
            return company?.Name ?? "MaidsFlow";
        }

        if (user.ProfessionalId.HasValue)
        {
            var prof = await _uow.Professionals.GetByIdAsync(user.ProfessionalId.Value);
            if (prof?.CompanyId != null)
            {
                var company = await _uow.Companies.GetByIdAsync(prof.CompanyId);
                return company?.Name ?? "MaidsFlow";
            }
        }

        return "MaidsFlow";
    }
}
