using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Exceptions;
using Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;

namespace Services.Email;

public class PasswordResetEmailService : IPasswordResetEmailService
{
    private readonly IUnitOfWork _uow;
    private readonly ISendGridEmailSender _emailSender;
    private readonly SendGridOptions _options;

    public PasswordResetEmailService(IUnitOfWork uow, ISendGridEmailSender emailSender, IOptions<SendGridOptions> options)
    {
        _uow = uow;
        _emailSender = emailSender;
        _options = options.Value;
    }

    public async Task SendPasswordResetEmailAsync(int userId, string resetUrl, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetById(userId);
        if (user == null)
            throw new KeyNotFoundException("Usuário não encontrado.");

        var companyName = "MaidsFlow";
        if (user.CompanyId.HasValue)
        {
            var c = await _uow.Companies.GetById(user.CompanyId.Value);
            if (c != null && !string.IsNullOrWhiteSpace(c.Name))
                companyName = c.Name;
        }

        var subject = string.IsNullOrWhiteSpace(_options.PasswordResetSubject)
            ? "Reset your MaidsFlow password"
            : _options.PasswordResetSubject;

        var rendered = PasswordResetEmailTemplate.Render(
            new PasswordResetEmailTemplate.Payload(
                CompanyName: companyName,
                UserName: user.Name,
                Email: user.Email,
                ResetUrl: resetUrl
            ),
            subject
        );

        await _emailSender.SendAsync(
            new SendGridEmailMessage(
                ToEmail: user.Email,
                Subject: rendered.Subject,
                PlainText: rendered.PlainText,
                Html: rendered.Html,
                ToName: user.Name
            ),
            ct
        );
}
}
