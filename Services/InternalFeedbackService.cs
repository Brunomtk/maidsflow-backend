using System;
using System.Threading.Tasks;
using Core.DTO.InternalFeedback;
using Core.Enums.InternalFeedback;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;

namespace Services
{
    public interface IInternalFeedbackService
    {
        Task<PagedResult<InternalFeedback>> GetPagedAsync(InternalFeedbackFiltersDTO filters);
        Task<InternalFeedback?> GetByIdAsync(int id);
        Task<InternalFeedback> CreateAsync(CreateInternalFeedbackDTO dto);
        Task<InternalFeedback?> UpdateAsync(int id, UpdateInternalFeedbackDTO dto);
        Task<bool> DeleteAsync(int id);
        Task<InternalFeedbackComment> AddCommentAsync(int feedbackId, CreateInternalFeedbackCommentDTO dto);
    }

    public class InternalFeedbackService : IInternalFeedbackService
    {
        private readonly IUnitOfWork _unitOfWork;

        public InternalFeedbackService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<PagedResult<InternalFeedback>> GetPagedAsync(InternalFeedbackFiltersDTO filters)
            => _unitOfWork.InternalFeedbacks.GetPagedAsync(filters);

        public Task<InternalFeedback?> GetByIdAsync(int id)
            => _unitOfWork.InternalFeedbacks.GetByIdAsync(id);

        public async Task<InternalFeedback> CreateAsync(CreateInternalFeedbackDTO dto)
        {
            var entity = new InternalFeedback
            {
                Title = dto.Title,
                ProfessionalId = dto.ProfessionalId,
                TeamId = dto.TeamId,
                Category = dto.Category,
                Status = dto.Status,
                Date = dto.Date,
                Description = dto.Description,
                Priority = dto.Priority,
                AssignedToId = dto.AssignedToId,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.InternalFeedbacks.Add(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<InternalFeedback?> UpdateAsync(int id, UpdateInternalFeedbackDTO dto)
        {
            var entity = await _unitOfWork.InternalFeedbacks.GetByIdAsync(id);
            if (entity == null) return null;

            if (!string.IsNullOrEmpty(dto.Title)) entity.Title = dto.Title;
            if (dto.ProfessionalId.HasValue) entity.ProfessionalId = dto.ProfessionalId.Value;
            if (dto.TeamId.HasValue) entity.TeamId = dto.TeamId.Value;
            if (!string.IsNullOrEmpty(dto.Category)) entity.Category = dto.Category;
            if (dto.Status.HasValue) entity.Status = dto.Status.Value;
            if (dto.Date.HasValue) entity.Date = dto.Date.Value;
            if (!string.IsNullOrEmpty(dto.Description)) entity.Description = dto.Description;
            if (dto.Priority.HasValue) entity.Priority = dto.Priority.Value;
            if (dto.AssignedToId.HasValue) entity.AssignedToId = dto.AssignedToId.Value;

            entity.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.InternalFeedbacks.Update(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _unitOfWork.InternalFeedbacks.GetByIdAsync(id);
            if (entity == null) return false;
            _unitOfWork.InternalFeedbacks.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<InternalFeedbackComment> AddCommentAsync(int feedbackId, CreateInternalFeedbackCommentDTO dto)
        {
            var feedback = await _unitOfWork.InternalFeedbacks.GetByIdAsync(feedbackId);
            if (feedback == null) throw new InvalidOperationException("Feedback not found");

            var comment = new InternalFeedbackComment
            {
                InternalFeedbackId = feedbackId,
                AuthorId = dto.AuthorId,
                Author = dto.Author,
                Text = dto.Text,
                Date = DateTime.UtcNow
            };

            feedback.Comments.Add(comment);
            feedback.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.InternalFeedbacks.Update(feedback);
            await _unitOfWork.SaveAsync();

            return comment;
        }
    }
}
