using System;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Review;
using Core.Models;
using Core.Exceptions;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Services.Security;

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
    }

    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public ReviewService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
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
                    Url = string.IsNullOrWhiteSpace(publicFormBaseUrl) ? null : $"{publicFormBaseUrl.TrimEnd('/')}/{existing.PublicToken.Value}"
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
