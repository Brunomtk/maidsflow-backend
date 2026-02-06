using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Services.Integrations.SendGrid;

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

        public ReviewRequestEmailService(IUnitOfWork uow, ISendGridEmailSender sender, IOptions<SendGridOptions> opt)
        {
            _uow = uow;
            _sender = sender;
            _opt = opt.Value;
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

            var (html, plain) = ReviewRequestEmailTemplate.Render(model);

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

            await _sender.SendAsync(msg, ct);
        }
    }
}
