using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Core.Enums.User;
using Core.Exceptions;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;
using Services.Security;

namespace Services.Email;

public class CredentialsEmailService : ICredentialsEmailService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeGuard _scope;
    private readonly ISendGridEmailSender _emailSender;
    private readonly SendGridOptions _options;

    public CredentialsEmailService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IScopeGuard scope,
        ISendGridEmailSender emailSender,
        IOptions<SendGridOptions> options)
    {
        _uow = uow;
        _currentUser = currentUser;
        _scope = scope;
        _emailSender = emailSender;
        _options = options.Value;
    }

    public async Task<SendCredentialsResult> SendUserCredentialsAsync(
        int userId,
        bool generateNewPassword,
        string? loginUrl,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
            throw new ForbiddenException("Você não tem permissão para enviar credenciais.");

        var user = await _uow.Users.GetByIdWithPermissions(userId);
        if (user == null)
            // O middleware mapeia KeyNotFoundException para 404.
            throw new KeyNotFoundException("Usuário não encontrado.");

        // Scope enforcement
        if (!_currentUser.IsAdmin)
        {
            if (!user.CompanyId.HasValue)
                throw new ForbiddenException("Usuário sem company.");

            await _scope.EnsureCompanyAccessAsync(user.CompanyId.Value);

            if (!string.IsNullOrWhiteSpace(user.Role) && user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Company não pode enviar credenciais para usuário admin.");
        }

        // Resolve Company name
        var companyName = await ResolveCompanyNameAsync(user);

        var pwdRegenerated = false;
        string? generatedPassword = null;
        string passwordToSend;

        if (generateNewPassword)
        {
            generatedPassword = GeneratePassword(12);
            passwordToSend = generatedPassword;
            pwdRegenerated = true;

            user.Password = Encrypt.EncryptPassword(generatedPassword);
            user.Onboarding = true;
            user.UpdatedDate = DateTime.UtcNow;
            _uow.Users.Update(user);
            _uow.Save();
        }
        else
        {
            // We do not have access to the plain password, so we must regenerate.
            // Keeping this explicit to avoid sending garbage.
            generatedPassword = GeneratePassword(12);
            passwordToSend = generatedPassword;
            pwdRegenerated = true;

            user.Password = Encrypt.EncryptPassword(generatedPassword);
            user.Onboarding = true;
            user.UpdatedDate = DateTime.UtcNow;
            _uow.Users.Update(user);
            _uow.Save();
        }

        var role = string.IsNullOrWhiteSpace(user.Role) ? "user" : user.Role;
        var url = string.IsNullOrWhiteSpace(loginUrl) ? _options.SupportUrl : loginUrl;
        var subject = string.IsNullOrWhiteSpace(_options.CredentialsSubject)
            ? "Your MaidsFlow access credentials"
            : _options.CredentialsSubject;

        var rendered = CredentialsEmailTemplate.Render(
            new CredentialsEmailTemplate.Payload(
                CompanyName: companyName,
                UserName: user.Name ?? "",
                Email: user.Email ?? "",
                Password: passwordToSend,
                Role: role,
                LoginUrl: url
            ),
            subject
        );

        var send = await _emailSender.SendAsync(new SendGridEmailMessage(
            ToEmail: user.Email ?? string.Empty,
            Subject: rendered.Subject,
            PlainText: rendered.PlainText,
            Html: rendered.Html,
            ToName: user.Name
        ), ct);

        return new SendCredentialsResult(
            UserId: user.Id,
            ToEmail: user.Email ?? string.Empty,
            PasswordRegenerated: pwdRegenerated,
            GeneratedPassword: generatedPassword,
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

    private static string GeneratePassword(int length)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "@#$%&_?!";

        var all = upper + lower + digits + symbols;

        // Ensure at least one from each group
        Span<char> pwd = stackalloc char[length];
        pwd[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        pwd[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        pwd[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        pwd[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (var i = 4; i < length; i++)
            pwd[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Shuffle
        for (var i = pwd.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (pwd[i], pwd[j]) = (pwd[j], pwd[i]);
        }

        return new string(pwd);
    }
}
