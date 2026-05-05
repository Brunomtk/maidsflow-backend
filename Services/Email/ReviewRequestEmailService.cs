using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;
using Services.Localization;

namespace Services.Email
{
    public interface IReviewRequestEmailService
    {
        Task SendReviewRequestAsync(
            int companyId,
            int customerId,
            string reviewUrl,
            string appointmentTitle,
            DateTime appointmentStartLocal,
            string? addressLine,
            CancellationToken ct = default);
    }

    public class ReviewRequestEmailService : IReviewRequestEmailService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISendGridEmailSender _sender;
        private readonly SendGridOptions _opt;
        private readonly IMessageLocalizer _loc;
        private readonly IRecipientLanguageResolver _langResolver;

        public ReviewRequestEmailService(
            IUnitOfWork uow,
            ISendGridEmailSender sender,
            IOptions<SendGridOptions> opt,
            IMessageLocalizer loc,
            IRecipientLanguageResolver langResolver)
        {
            _uow = uow;
            _sender = sender;
            _opt = opt.Value;
            _loc = loc;
            _langResolver = langResolver;
        }

        public async Task SendReviewRequestAsync(
            int companyId,
            int customerId,
            string reviewUrl,
            string appointmentTitle,
            DateTime appointmentStartLocal,
            string? addressLine,
            CancellationToken ct = default)
        {
            var company = await _uow.Companies.GetById(companyId);
            if (company == null) return;

            var customer = await _uow.Customers.GetById(customerId);
            if (customer == null) return;
            if (!customer.ReceiveEmail) return;
            if (string.IsNullOrWhiteSpace(customer.Email)) return;

            var model = new ReviewRequestEmailTemplate.Model(
                CustomerName: customer.Name,
                CompanyName: company.Name,
                AppointmentTitle: appointmentTitle,
                AppointmentStartLocal: appointmentStartLocal,
                AddressLine: addressLine,
                ReviewUrl: reviewUrl,
                SupportUrl: _opt.SupportUrl
            );

            var language = await _langResolver.ForCustomerAsync(customerId, ct);

            var (html, plain) = ReviewRequestEmailTemplate.Render(model, _loc, language);

            var subject = string.IsNullOrWhiteSpace(_opt.ReviewRequestSubject)
                ? "How was your service?"
                : _opt.ReviewRequestSubject.Trim();

            var msg = new SendGridEmailMessage(
                ToEmail: customer.Email,
                Subject: subject,
                PlainText: plain,
                Html: html,
                ToName: customer.Name
            );

            var result = await _sender.SendAsync(msg, ct);
            if (!result.Ok)
            {
                var details = string.IsNullOrWhiteSpace(result.ResponseBody)
                    ? result.Error
                    : result.ResponseBody;

                throw new InvalidOperationException(
                    $"Failed to send review request email via SendGrid. StatusCode={result.StatusCode}. Details={details}");
            }
        }
    }
}
