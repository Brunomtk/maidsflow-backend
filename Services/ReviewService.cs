using System;
using System.Threading.Tasks;
using Core.DTO.Review;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;

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
    }

    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReviewService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<PagedResult<Review>> GetPagedAsync(ReviewFiltersDTO filters)
            => _unitOfWork.Reviews.GetPagedAsync(filters);

        public Task<Review?> GetByIdAsync(int id)
            => _unitOfWork.Reviews.GetByIdAsync(id);

        public async Task<Review> CreateAsync(CreateReviewDTO dto)
        {
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
                Rating = dto.Rating,
                Comment = dto.Comment,
                Date = dto.Date,
                ServiceType = dto.ServiceType,
                Status = dto.Status,
                Response = dto.Response,
                ResponseDate = dto.ResponseDate,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.Reviews.Add(review);
            await _unitOfWork.SaveAsync();
            return review;
        }

        public async Task<Review?> UpdateAsync(int id, UpdateReviewDTO dto)
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null) return null;

            if (!string.IsNullOrEmpty(dto.CustomerId)) review.CustomerId = dto.CustomerId;
            if (!string.IsNullOrEmpty(dto.CustomerName)) review.CustomerName = dto.CustomerName;
            if (!string.IsNullOrEmpty(dto.ProfessionalId)) review.ProfessionalId = dto.ProfessionalId;
            if (!string.IsNullOrEmpty(dto.ProfessionalName)) review.ProfessionalName = dto.ProfessionalName;
            if (!string.IsNullOrEmpty(dto.TeamId)) review.TeamId = dto.TeamId;
            if (!string.IsNullOrEmpty(dto.TeamName)) review.TeamName = dto.TeamName;
            if (!string.IsNullOrEmpty(dto.CompanyId)) review.CompanyId = dto.CompanyId;
            if (!string.IsNullOrEmpty(dto.CompanyName)) review.CompanyName = dto.CompanyName;
            if (!string.IsNullOrEmpty(dto.AppointmentId)) review.AppointmentId = dto.AppointmentId;
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
            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null) return false;

            _unitOfWork.Reviews.Delete(review);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<Review?> RespondAsync(int id, string response)
        {
            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null) return null;

            review.Response = response;
            review.ResponseDate = DateTime.UtcNow;
            review.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Reviews.Update(review);
            await _unitOfWork.SaveAsync();
            return review;
        }
    }
}
