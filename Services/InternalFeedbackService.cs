using System;
using System.Threading.Tasks;
using Core.DTO.InternalFeedback;
using Core.Enums.InternalFeedback;
using Core.Models;
using Infrastructure.Repositories;
using Infrastructure.ServiceExtension;
using Services.Security;
using Core.Exceptions;

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
        private readonly Infrastructure.Repositories.IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public InternalFeedbackService(Infrastructure.Repositories.IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<PagedResult<InternalFeedback>> GetPagedAsync(InternalFeedbackFiltersDTO filters)
{
    if (!_currentUser.IsAdmin)
    {
        var companyId = await _scope.GetScopedCompanyIdAsync();
        if (companyId.HasValue) filters.CompanyId = companyId.Value;

        if (_currentUser.IsProfessional)
        {
            var professionalId = await _scope.GetScopedProfessionalIdAsync();
            if (professionalId.HasValue) filters.ProfessionalId = professionalId.Value;
        }
    }

    return await _unitOfWork.InternalFeedbacks.GetPagedAsync(filters);
}


        public async Task<InternalFeedback?> GetByIdAsync(int id)
{
    var entity = await _unitOfWork.InternalFeedbacks.GetByIdAsync(id);
    if (entity == null) return null;

    if (!_currentUser.IsAdmin)
        await _scope.EnsureProfessionalInCompanyAsync(entity.ProfessionalId);

    if (_currentUser.IsProfessional)
    {
        var pid = await _scope.GetScopedProfessionalIdAsync();
        if (!pid.HasValue || pid.Value != entity.ProfessionalId)
            throw new ForbiddenException("Você não tem permissão para acessar este feedback.");
    }

    return entity;
}


        public async Task<InternalFeedback> CreateAsync(CreateInternalFeedbackDTO dto)
{
    if (!_currentUser.IsAdmin)
    {
        // Para company: garante que o ProfessionalId pertence à company.
        // Para professional: força ProfessionalId próprio.
        if (_currentUser.IsProfessional)
        {
            var pid = await _scope.GetScopedProfessionalIdAsync();
            if (!pid.HasValue) throw new ForbiddenException("Escopo de profissional inválido.");
            dto.ProfessionalId = pid.Value;
        }

        await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId);
    }


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

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureProfessionalInCompanyAsync(entity.ProfessionalId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != entity.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para editar este feedback.");
                }
            }

            if (!string.IsNullOrEmpty(dto.Title)) entity.Title = dto.Title;
            if (dto.ProfessionalId.HasValue)
            {
                if (_currentUser.IsProfessional)
                    throw new ForbiddenException("Profissional não pode mudar o responsável do feedback.");

                await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId.Value);
                entity.ProfessionalId = dto.ProfessionalId.Value;
            }
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

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureProfessionalInCompanyAsync(entity.ProfessionalId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != entity.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para excluir este feedback.");
                }
            }

            _unitOfWork.InternalFeedbacks.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<InternalFeedbackComment> AddCommentAsync(int feedbackId, CreateInternalFeedbackCommentDTO dto)
        {
            var feedback = await _unitOfWork.InternalFeedbacks.GetByIdAsync(feedbackId);
            if (feedback == null) throw new InvalidOperationException("Feedback not found");

            if (!_currentUser.IsAdmin)
            {
                await _scope.EnsureProfessionalInCompanyAsync(feedback.ProfessionalId);

                if (_currentUser.IsProfessional)
                {
                    var pid = await _scope.GetScopedProfessionalIdAsync();
                    if (!pid.HasValue || pid.Value != feedback.ProfessionalId)
                        throw new ForbiddenException("Você não tem permissão para comentar neste feedback.");
                }
            }

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
