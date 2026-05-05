using System;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Review;
using Core.Models;
using Core.Exceptions;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Services.Security;
using Services.Email;
using Services.Integrations.SendGrid;
using Services.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Core.Enums.Messaging;
using System.Text.Json;

namespace Services
{
    public interface IReviewService
    {
        Task<PagedResult<Review>> GetPagedAsync(ReviewFiltersDTO filters);
        Task<Review?> GetByIdAsync(int id);
        Task<Review> CreateAsync(CreateReviewDTO dto);
        Task<Review?> UpdateAsync(int id, UpdateReviewDTO dto);
        Task<bool> DeleteAsync(int id);
        Task<Review?> RespondAsync(int id, string response);

        Task<ReviewLinkDTO> GetOrCreateLinkForAppointmentAsync(int appointmentId, string? publicFormBaseUrl = null);
        Task<PublicReviewInfoDTO?> GetPublicInfoAsync(Guid token);
        Task<Review?> SubmitPublicAsync(Guid token, PublicReviewSubmitDTO dto);
        Task<ReviewEmailDispatchDTO> SendReviewLinkByEmailAsync(int appointmentId, string? publicFormBaseUrl = null);
    }

    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly IReviewRequestEmailService _reviewRequestEmailService;
        private readonly SendGridOptions _sendGridOptions;
        private readonly IConfiguration _configuration;
        private readonly IMessageLocalizer _loc;
        private readonly IRecipientLanguageResolver _langResolver;

        public ReviewService(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            IScopeGuard scope,
            IReviewRequestEmailService reviewRequestEmailService,
            IOptions<SendGridOptions> sendGridOptions,
            IConfiguration configuration,
            IMessageLocalizer loc,
            IRecipientLanguageResolver langResolver)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
            _reviewRequestEmailService = reviewRequestEmailService;
            _sendGridOptions = sendGridOptions.Value;
            _configuration = configuration;
            _loc = loc;
            _langResolver = langResolver;
        }

        public async Task<PagedResult<Review>> GetPagedAsync(ReviewFiltersDTO filters)
        {
            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (companyId.HasValue) filters.CompanyId = companyId.Value;

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (pid.HasValue) filters.ProfessionalId = pid.Value;
                }
            }

            return await _unitOfWork.Reviews.GetPagedAsync(filters);
        }

        public async Task<Review?> GetByIdAsync(int id)
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null) return null;

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureCompanyAccessAsync(review.CompanyId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != review.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para acessar este review.");
                }
            }

            return review;
        }

        public async Task<Review> CreateAsync(CreateReviewDTO dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode criar review.");

            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (companyId.HasValue) dto.CompanyId = companyId.Value;
            }

            // garantir que o appointment pertence à company + normalizar CustomerAddressId
            Appointment? apptForAddress = null;
            if (!_currentUser.IsAdmin)
            {
                apptForAddress = await _unitOfWork.Appointments.GetById(dto.AppointmentId);
                if (apptForAddress != null) await _scope.EnsureCompanyAccessAsync(apptForAddress.CompanyId);
            }

            if (apptForAddress != null && apptForAddress.CustomerAddressId.HasValue)
            {
                if (!dto.CustomerAddressId.HasValue)
                {
                    dto.CustomerAddressId = apptForAddress.CustomerAddressId.Value;
                }
                else if (dto.CustomerAddressId.Value != apptForAddress.CustomerAddressId.Value)
                {
                    throw new BadRequestException("CustomerAddressId não bate com o endereço do appointment.");
                }
            }

            if (dto.CustomerAddressId.HasValue)
            {
                await _scope.EnsureCustomerAddressAccessAsync(dto.CustomerAddressId.Value);

                var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(dto.CustomerAddressId.Value);
                if (addr == null)
                    throw new BadRequestException("CustomerAddressId inválido.");
                if (addr.CustomerId != dto.CustomerId)
                    throw new BadRequestException("CustomerAddressId não pertence ao CustomerId informado.");
            }

            var review = new Review
            {
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                ProfessionalId = dto.ProfessionalId,
                ProfessionalName = dto.ProfessionalName,
                TeamId = dto.TeamId,
                TeamName = dto.TeamName,
                CompanyId = dto.CompanyId,
                CompanyName = dto.CompanyName,
                AppointmentId = dto.AppointmentId,
                CustomerAddressId = dto.CustomerAddressId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                Date = dto.Date,
                ServiceType = dto.ServiceType,
                Status = dto.Status,
                Response = dto.Response,
                ResponseDate = dto.ResponseDate,
                PublicToken = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.Reviews.Add(review);
            await _unitOfWork.SaveAsync();
            return review;
        }

        public async Task<Review?> UpdateAsync(int id, UpdateReviewDTO dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode editar review.");

            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null) return null;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(review.CompanyId);

            if (dto.CustomerId.HasValue) review.CustomerId = dto.CustomerId.Value;
            if (!string.IsNullOrEmpty(dto.CustomerName)) review.CustomerName = dto.CustomerName;
            if (dto.ProfessionalId.HasValue) review.ProfessionalId = dto.ProfessionalId;
            if (!string.IsNullOrEmpty(dto.ProfessionalName)) review.ProfessionalName = dto.ProfessionalName;
            if (dto.TeamId.HasValue) review.TeamId = dto.TeamId;
            if (!string.IsNullOrEmpty(dto.TeamName)) review.TeamName = dto.TeamName;
            if (_currentUser.IsAdmin && dto.CompanyId.HasValue) review.CompanyId = dto.CompanyId.Value;
            if (!string.IsNullOrEmpty(dto.CompanyName)) review.CompanyName = dto.CompanyName;
            if (dto.AppointmentId.HasValue) review.AppointmentId = dto.AppointmentId.Value;
            if (dto.CustomerAddressId.HasValue)
            {
                await _scope.EnsureCustomerAddressAccessAsync(dto.CustomerAddressId.Value);

                var targetCustomerId = dto.CustomerId ?? review.CustomerId;
                var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(dto.CustomerAddressId.Value);
                if (addr == null) throw new BadRequestException("CustomerAddressId inválido.");
                if (addr.CustomerId != targetCustomerId)
                    throw new BadRequestException("CustomerAddressId não pertence ao CustomerId do review.");
                review.CustomerAddressId = dto.CustomerAddressId.Value;
            }

            if (dto.Rating.HasValue) review.Rating = dto.Rating.Value;
            if (!string.IsNullOrEmpty(dto.Comment)) review.Comment = dto.Comment;
            if (dto.Date.HasValue) review.Date = dto.Date.Value;
            if (!string.IsNullOrEmpty(dto.ServiceType)) review.ServiceType = dto.ServiceType;
            if (dto.Status.HasValue) review.Status = dto.Status.Value;
            if (!string.IsNullOrEmpty(dto.Response)) review.Response = dto.Response;
            if (dto.ResponseDate.HasValue) review.ResponseDate = dto.ResponseDate.Value;

            review.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.Reviews.Update(review);
            await _unitOfWork.SaveAsync();
            return review;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não pode excluir review.");

            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null) return false;

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(review.CompanyId);

            _unitOfWork.Reviews.Delete(review);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<Review?> RespondAsync(int id, string response)
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null) return null;

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureCompanyAccessAsync(review.CompanyId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != review.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para responder este review.");
                }
            }

            review.Response = response;
            review.ResponseDate = DateTime.UtcNow;
            review.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Reviews.Update(review);
            await _unitOfWork.SaveAsync();
            return review;
        }

        public async Task<ReviewEmailDispatchDTO> SendReviewLinkByEmailAsync(int appointmentId, string? publicFormBaseUrl = null)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Professional cannot send review-request emails.");

            var appointment = await _unitOfWork.Appointments.GetById(appointmentId);
            if (appointment == null)
                throw new NotFoundException("Appointment not found.");

            if (!_currentUser.IsAdmin)
                await _scope.EnsureCompanyAccessAsync(appointment.CompanyId);

            if (!appointment.CustomerId.HasValue)
                throw new BadRequestException("The selected appointment does not have a customer linked to it.");

            var customer = await _unitOfWork.Customers.GetById(appointment.CustomerId.Value);
            if (customer == null)
                throw new NotFoundException("Customer not found.");

            if (customer.CompanyId != appointment.CompanyId)
                throw new BadRequestException("The appointment customer does not belong to the same company.");

            if (!customer.ReceiveEmail)
                throw new BadRequestException("This customer has email notifications disabled.");

            if (string.IsNullOrWhiteSpace(customer.Email))
                throw new BadRequestException("This customer does not have an email address registered.");

            var company = await _unitOfWork.Companies.GetById(appointment.CompanyId);
            if (company == null)
                throw new NotFoundException("Company not found.");

            var resolvedBaseUrl = ResolvePublicReviewFormBaseUrl(publicFormBaseUrl);
            if (string.IsNullOrWhiteSpace(resolvedBaseUrl))
                throw new BadRequestException("Public review form URL is not configured. Set SendGrid:PublicReviewFormBaseUrl or AutoReviews:ReviewRequestAfterComplete:PublicReviewFormBaseUrl.");

            var link = await GetOrCreateLinkForAppointmentAsync(appointmentId, resolvedBaseUrl);
            if (string.IsNullOrWhiteSpace(link.Url))
                throw new BadRequestException("Failed to generate the public review URL.");

            var appointmentTitle = string.IsNullOrWhiteSpace(appointment.Title) ? "Your service" : appointment.Title.Trim();
            var addressLine = await BuildAppointmentAddressLineAsync(appointment);
            var subject = string.IsNullOrWhiteSpace(_sendGridOptions.ReviewRequestSubject)
                ? "How was your service?"
                : _sendGridOptions.ReviewRequestSubject.Trim();
            var supportUrl = string.IsNullOrWhiteSpace(_sendGridOptions.SupportUrl) ? string.Empty : _sendGridOptions.SupportUrl.Trim();
            var customerLanguage = await _langResolver.ForCustomerAsync(customer.Id);
            var (_, plainText) = ReviewRequestEmailTemplate.Render(new ReviewRequestEmailTemplate.Model(
                CustomerName: customer.Name ?? string.Empty,
                CompanyName: company.Name ?? string.Empty,
                AppointmentTitle: appointmentTitle,
                AppointmentStartLocal: appointment.Start,
                AddressLine: addressLine,
                ReviewUrl: link.Url!,
                SupportUrl: supportUrl
            ), _loc, customerLanguage);
            var sentAtUtc = DateTime.UtcNow;

            try
            {
                await _reviewRequestEmailService.SendReviewRequestAsync(
                    companyId: appointment.CompanyId,
                    customerId: customer.Id,
                    reviewUrl: link.Url!,
                    appointmentTitle: appointmentTitle,
                    appointmentStartLocal: appointment.Start,
                    addressLine: addressLine);

                await AddReviewEmailLogAsync(
                    appointment: appointment,
                    occurrenceStartUtc: appointment.Start.Kind == DateTimeKind.Utc ? appointment.Start : null,
                    occurrenceEndUtc: appointment.End.Kind == DateTimeKind.Utc ? appointment.End : null,
                    recipientEmail: customer.Email!.Trim(),
                    subject: subject,
                    bodyText: plainText,
                    reviewUrl: link.Url!,
                    reviewId: link.ReviewId,
                    customerId: customer.Id,
                    customerName: customer.Name,
                    status: AppointmentMessageStatus.Sent,
                    providerStatus: "Sent",
                    sentAtUtc: sentAtUtc,
                    lastError: null,
                    lastErrorRaw: null,
                    isManual: true);
            }
            catch (Exception ex)
            {
                await AddReviewEmailLogAsync(
                    appointment: appointment,
                    occurrenceStartUtc: appointment.Start.Kind == DateTimeKind.Utc ? appointment.Start : null,
                    occurrenceEndUtc: appointment.End.Kind == DateTimeKind.Utc ? appointment.End : null,
                    recipientEmail: customer.Email!.Trim(),
                    subject: subject,
                    bodyText: plainText,
                    reviewUrl: link.Url!,
                    reviewId: link.ReviewId,
                    customerId: customer.Id,
                    customerName: customer.Name,
                    status: AppointmentMessageStatus.Failed,
                    providerStatus: "Failed",
                    sentAtUtc: null,
                    lastError: ex.Message,
                    lastErrorRaw: ex.ToString(),
                    isManual: true);
                throw;
            }

            return new ReviewEmailDispatchDTO
            {
                ReviewId = link.ReviewId,
                Token = link.Token,
                Url = link.Url!,
                AppointmentId = appointment.Id,
                CustomerId = customer.Id,
                CustomerName = customer.Name ?? string.Empty,
                RecipientEmail = customer.Email!.Trim(),
                Subject = subject,
                SentAtUtc = sentAtUtc
            };
        }

        private async Task AddReviewEmailLogAsync(
            Appointment appointment,
            DateTime? occurrenceStartUtc,
            DateTime? occurrenceEndUtc,
            string recipientEmail,
            string subject,
            string bodyText,
            string reviewUrl,
            int reviewId,
            int customerId,
            string? customerName,
            AppointmentMessageStatus status,
            string providerStatus,
            DateTime? sentAtUtc,
            string? lastError,
            string? lastErrorRaw,
            bool isManual)
        {
            var nextAttempt = await _unitOfWork.AppointmentMessageLogs.GetNextAttemptAsync(
                appointment.Id,
                AppointmentMessageKind.ReviewRequestEmail,
                AppointmentMessageChannel.Email,
                occurrenceStartUtc,
                occurrenceEndUtc);

            var payloadJson = JsonSerializer.Serialize(new
            {
                reviewId,
                reviewUrl,
                appointmentId = appointment.Id,
                appointmentTitle = appointment.Title,
                appointmentStart = appointment.Start,
                appointmentEnd = appointment.End,
                companyId = appointment.CompanyId,
                customerId,
                customerName,
                customerAddressId = appointment.CustomerAddressId,
                occurrenceStartUtc,
                occurrenceEndUtc,
                manual = isManual
            });

            await _unitOfWork.AppointmentMessageLogs.Add(new AppointmentMessageLog
            {
                AppointmentId = appointment.Id,
                SeriesId = appointment.SeriesId,
                OccurrenceStartUtc = occurrenceStartUtc,
                OccurrenceEndUtc = occurrenceEndUtc,
                Kind = AppointmentMessageKind.ReviewRequestEmail,
                Channel = AppointmentMessageChannel.Email,
                Status = status,
                ScheduledForUtc = sentAtUtc ?? DateTime.UtcNow,
                SentAtUtc = sentAtUtc,
                Attempt = nextAttempt,
                RequestedByUserId = _currentUser.UserId,
                RequestedByRole = _currentUser.Role,
                RecipientEmail = recipientEmail,
                Subject = subject,
                BodyText = bodyText,
                TemplateKey = "review-request-email",
                PayloadJson = payloadJson,
                Provider = "SendGrid",
                ProviderStatus = providerStatus,
                LastError = lastError,
                LastErrorRaw = lastErrorRaw,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            });

            await _unitOfWork.SaveAsync();
        }

        private string? ResolvePublicReviewFormBaseUrl(string? preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred.Trim();

            if (!string.IsNullOrWhiteSpace(_sendGridOptions.PublicReviewFormBaseUrl))
                return _sendGridOptions.PublicReviewFormBaseUrl.Trim();

            var fallback = _configuration["AutoReviews:ReviewRequestAfterComplete:PublicReviewFormBaseUrl"];
            return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
        }

        private async Task<string?> BuildAppointmentAddressLineAsync(Appointment appointment)
        {
            if (appointment.CustomerAddressId.HasValue)
            {
                var address = await _unitOfWork.CustomerAddresses.GetByIdAsync(appointment.CustomerAddressId.Value);
                if (address != null)
                {
                    var parts = new[]
                    {
                        address.AddressLine1,
                        address.AddressLine2,
                        address.City,
                        address.State,
                        address.ZipCode
                    }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .ToArray();

                    if (parts.Length > 0)
                        return string.Join(", ", parts);
                }
            }

            return string.IsNullOrWhiteSpace(appointment.Address) ? null : appointment.Address.Trim();
        }

        // ===== Public (AllowAnonymous no Controller) =====

        public async Task<ReviewLinkDTO> GetOrCreateLinkForAppointmentAsync(int appointmentId, string? publicFormBaseUrl = null)
        {
            var appt = await _unitOfWork.Appointments.GetById(appointmentId);
            if (appt == null)
                throw new InvalidOperationException("Appointment not found.");

            if (!appt.CustomerId.HasValue)
                throw new InvalidOperationException("Appointment has no CustomerId.");

            var existing = await _unitOfWork.Reviews.GetByAppointmentIdAsync(appointmentId);
            if (existing != null)
            {
                if (existing.PublicToken == null)
                {
                    existing.PublicToken = Guid.NewGuid();
                    existing.UpdatedDate = DateTime.UtcNow;
                    _unitOfWork.Reviews.Update(existing);
                    await _unitOfWork.SaveAsync();
                }

                return new ReviewLinkDTO
                {
                    ReviewId = existing.Id,
                    Token = existing.PublicToken.Value,
                    Url = string.IsNullOrWhiteSpace(publicFormBaseUrl) ? null : ReviewPublicLinkBuilder.Build(publicFormBaseUrl, existing.PublicToken.Value)
                };
            }

            var customer = await _unitOfWork.Customers.GetById(appt.CustomerId.Value);
            var company = await _unitOfWork.Companies.GetById(appt.CompanyId);

            int? professionalId = null;
            string? professionalName = null;
            if (appt.ProfessionalIds != null && appt.ProfessionalIds.Count > 0)
            {
                professionalId = appt.ProfessionalIds[0];
                var pro = await _unitOfWork.Professionals.GetById(professionalId.Value);
                professionalName = pro?.Name;
            }

            var token = Guid.NewGuid();
            var review = new Review
            {
                CustomerId = appt.CustomerId.Value,
                CustomerAddressId = appt.CustomerAddressId,
                CustomerName = customer?.Name,
                ProfessionalId = professionalId,
                ProfessionalName = professionalName,
                TeamId = appt.TeamId,
                TeamName = null,
                CompanyId = appt.CompanyId,
                CompanyName = company?.Name,
                AppointmentId = appt.Id,
                Date = appt.Start,
                ServiceType = appt.Type.ToString(),
                Status = Core.Enums.ReviewStatus.Pending,
                Rating = 0,
                Comment = null,
                PublicToken = token,
                SubmittedAt = null,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.Reviews.Add(review);
            await _unitOfWork.SaveAsync();

            return new ReviewLinkDTO
            {
                ReviewId = review.Id,
                Token = token,
                Url = string.IsNullOrWhiteSpace(publicFormBaseUrl) ? null : $"{publicFormBaseUrl.TrimEnd('/')}/{token}"
            };
        }

        public async Task<PublicReviewInfoDTO?> GetPublicInfoAsync(Guid token)
        {
            var review = await _unitOfWork.Reviews.GetByPublicTokenAsync(token);
            if (review == null) return null;

            return new PublicReviewInfoDTO
            {
                Token = token,
                ReviewId = review.Id,
                AppointmentId = review.AppointmentId,
                CustomerId = review.CustomerId,
                CustomerAddressId = review.CustomerAddressId,
                CompanyId = review.CompanyId,
                CompanyName = review.CompanyName,
                CustomerName = review.CustomerName,
                ProfessionalName = review.ProfessionalName,
                AppointmentStart = review.Date,
                Status = review.Status,
                CanSubmit = review.Status == Core.Enums.ReviewStatus.Pending,
                Rating = review.Status == Core.Enums.ReviewStatus.Pending ? null : review.Rating,
                Comment = review.Status == Core.Enums.ReviewStatus.Pending ? null : review.Comment,
                SubmittedAt = review.SubmittedAt
            };
        }

        public async Task<Review?> SubmitPublicAsync(Guid token, PublicReviewSubmitDTO dto)
        {
            var review = await _unitOfWork.Reviews.GetByPublicTokenAsync(token);
            if (review == null) return null;

            if (review.Status != Core.Enums.ReviewStatus.Pending)
                throw new InvalidOperationException("This review link was already used.");

            if (dto.Rating < 1 || dto.Rating > 5)
                throw new ArgumentOutOfRangeException(nameof(dto.Rating), "Rating must be between 1 and 5.");

            review.Rating = dto.Rating;
            review.Comment = dto.Comment;
            review.Status = Core.Enums.ReviewStatus.Published;
            review.SubmittedAt = DateTime.UtcNow;
            review.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.Reviews.Update(review);
            await _unitOfWork.SaveAsync();
            return review;
        }
    }
}
