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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public InternalFeedbackService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
        }

        private async Task EnsureCanAccessAsync(InternalFeedback feedback, bool forWrite = false)
        {
            if (_currentUser.IsAdmin) return;

            if (_currentUser.IsPropertyManager)
            {
                if (!feedback.CustomerId.HasValue)
                    throw new ForbiddenException("Você não tem permissão para acessar este feedback.");

                await _scope.EnsureCustomerInCompanyAsync(feedback.CustomerId.Value);

                if (feedback.CustomerAddressId.HasValue)
                    await _scope.EnsureCustomerAddressAccessAsync(feedback.CustomerAddressId.Value);

                return;
            }

            await _scope.EnsureProfessionalInCompanyAsync(feedback.ProfessionalId);

            if (_currentUser.IsProfessional)
            {
                var pid = await _scope.GetScopedProfessionalIdAsync();
                if (!pid.HasValue || pid.Value != feedback.ProfessionalId)
                    throw new ForbiddenException("Você não tem permissão para acessar este feedback.");
            }
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

                if (_currentUser.IsPropertyManager)
                {
                    var customerId = await _scope.GetScopedCustomerIdAsync();
                    if (customerId.HasValue) filters.CustomerId = customerId.Value;
                }
            }

            return await _unitOfWork.InternalFeedbacks.GetPagedAsync(filters);
        }

        public async Task<InternalFeedback?> GetByIdAsync(int id)
        {
            var entity = await _unitOfWork.InternalFeedbacks.GetByIdAsync(id);
            if (entity == null) return null;

            await EnsureCanAccessAsync(entity);
            return entity;
        }

        public async Task<InternalFeedback> CreateAsync(CreateInternalFeedbackDTO dto)
        {
            if (!_currentUser.IsAdmin)
            {
                if (_currentUser.IsPropertyManager)
                {
                    var scopedCustomerId = await _scope.GetScopedCustomerIdAsync();
                    if (!scopedCustomerId.HasValue)
                        throw new ForbiddenException("Escopo de cliente inválido.");

                    if (!dto.CustomerId.HasValue || dto.CustomerId.Value <= 0)
                        dto.CustomerId = scopedCustomerId.Value;
                    else if (dto.CustomerId.Value != scopedCustomerId.Value)
                        throw new ForbiddenException("Você não tem permissão para criar feedback para este cliente.");

                    // If appointment provided, infer key ids and validate relationship
                    if (dto.AppointmentId.HasValue)
                    {
                        var appt = await _unitOfWork.Appointments.GetById(dto.AppointmentId.Value);
                        if (appt == null)
                            throw new BadRequestException("AppointmentId inválido.");

                        if (!appt.CustomerId.HasValue || appt.CustomerId.Value != scopedCustomerId.Value)
                            throw new ForbiddenException("Você não tem permissão para criar feedback para este agendamento.");

                        if (appt.CustomerAddressId.HasValue)
                        {
                            if (!dto.CustomerAddressId.HasValue)
                                dto.CustomerAddressId = appt.CustomerAddressId.Value;
                            else if (dto.CustomerAddressId.Value != appt.CustomerAddressId.Value)
                                throw new BadRequestException("CustomerAddressId não bate com o endereço do appointment.");
                        }

                        // infer professional/team (best effort)
                        if (appt.ProfessionalIds != null && appt.ProfessionalIds.Count > 0)
                            dto.ProfessionalId = appt.ProfessionalIds[0];

                        if (appt.TeamId.HasValue && appt.TeamId.Value > 0)
                            dto.TeamId = appt.TeamId.Value;
                    }

                    if (dto.CustomerAddressId.HasValue)
                    {
                        await _scope.EnsureCustomerAddressAccessAsync(dto.CustomerAddressId.Value);

                        var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(dto.CustomerAddressId.Value);
                        if (addr == null)
                            throw new BadRequestException("CustomerAddressId inválido.");
                        if (addr.CustomerId != dto.CustomerId.Value)
                            throw new BadRequestException("CustomerAddressId não pertence ao CustomerId informado.");
                    }

                    // For PM, ProfessionalId/TeamId might be 0 if not inferred. Keep behavior strict if missing.
                    if (dto.ProfessionalId <= 0)
                        throw new BadRequestException("ProfessionalId é obrigatório (ou informe AppointmentId para inferir).");
                    if (dto.TeamId <= 0)
                        dto.TeamId = 0; // optional: no FK, keep 0
                }
                else
                {
                    // Company / Professional users
                    if (_currentUser.IsProfessional)
                    {
                        var pid = await _scope.GetScopedProfessionalIdAsync();
                        if (pid.HasValue) dto.ProfessionalId = pid.Value;
                    }

                    await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId);

                    if (dto.AppointmentId.HasValue)
                    {
                        await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId.Value);

                        var appt = await _unitOfWork.Appointments.GetById(dto.AppointmentId.Value);
                        if (appt != null)
                        {
                            if (appt.CustomerId.HasValue && (!dto.CustomerId.HasValue || dto.CustomerId.Value <= 0))
                                dto.CustomerId = appt.CustomerId.Value;

                            if (appt.CustomerAddressId.HasValue && !dto.CustomerAddressId.HasValue)
                                dto.CustomerAddressId = appt.CustomerAddressId.Value;
                        }
                    }

                    if (dto.CustomerId.HasValue)
                        await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId.Value);

                    if (dto.CustomerAddressId.HasValue)
                    {
                        await _scope.EnsureCustomerAddressAccessAsync(dto.CustomerAddressId.Value);

                        if (dto.CustomerId.HasValue)
                        {
                            var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(dto.CustomerAddressId.Value);
                            if (addr == null) throw new BadRequestException("CustomerAddressId inválido.");
                            if (addr.CustomerId != dto.CustomerId.Value)
                                throw new BadRequestException("CustomerAddressId não pertence ao CustomerId informado.");
                        }
                    }
                }
            }

            var entity = new InternalFeedback
            {
                Title = dto.Title,
                ProfessionalId = dto.ProfessionalId,
                TeamId = dto.TeamId,
                AppointmentId = dto.AppointmentId,
                CustomerId = dto.CustomerId,
                CustomerAddressId = dto.CustomerAddressId,
                Category = dto.Category,
                Status = dto.Status,
                Date = dto.Date == default ? DateTime.UtcNow : dto.Date,
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

            await EnsureCanAccessAsync(entity, forWrite: true);

            if (!_currentUser.IsAdmin)
            {
                if (_currentUser.IsPropertyManager)
                {
                    var scopedCustomerId = await _scope.GetScopedCustomerIdAsync();
                    if (!scopedCustomerId.HasValue || !entity.CustomerId.HasValue || entity.CustomerId.Value != scopedCustomerId.Value)
                        throw new ForbiddenException("Você não tem permissão para editar este feedback.");

                    // PM cannot move feedback to another customer
                    if (dto.CustomerId.HasValue && dto.CustomerId.Value != scopedCustomerId.Value)
                        throw new ForbiddenException("Você não pode alterar o CustomerId deste feedback.");
                }
                else
                {
                    // professional/company: keep professional constrained
                    if (_currentUser.IsProfessional)
                    {
                        var pid = await _scope.GetScopedProfessionalIdAsync();
                        if (pid.HasValue && dto.ProfessionalId.HasValue && dto.ProfessionalId.Value != pid.Value)
                            throw new ForbiddenException("Você não pode alterar o ProfessionalId deste feedback.");
                    }

                    if (dto.ProfessionalId.HasValue)
                        await _scope.EnsureProfessionalInCompanyAsync(dto.ProfessionalId.Value);

                    if (dto.CustomerId.HasValue)
                        await _scope.EnsureCustomerInCompanyAsync(dto.CustomerId.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Title)) entity.Title = dto.Title;
            if (dto.ProfessionalId.HasValue) entity.ProfessionalId = dto.ProfessionalId.Value;
            if (dto.TeamId.HasValue) entity.TeamId = dto.TeamId.Value;
            if (dto.AppointmentId.HasValue)
            {
                if (!_currentUser.IsAdmin)
                {
                    if (_currentUser.IsPropertyManager)
                    {
                        var appt = await _unitOfWork.Appointments.GetById(dto.AppointmentId.Value);
                        if (appt == null) throw new BadRequestException("AppointmentId inválido.");
                        if (!appt.CustomerId.HasValue || !entity.CustomerId.HasValue || appt.CustomerId.Value != entity.CustomerId.Value)
                            throw new ForbiddenException("Você não tem permissão para vincular este appointment.");
                    }
                    else
                    {
                        await _scope.EnsureAppointmentAccessAsync(dto.AppointmentId.Value);
                    }
                }

                entity.AppointmentId = dto.AppointmentId.Value;
            }

            if (dto.CustomerId.HasValue) entity.CustomerId = dto.CustomerId.Value;

            if (dto.CustomerAddressId.HasValue)
            {
                if (!_currentUser.IsAdmin)
                    await _scope.EnsureCustomerAddressAccessAsync(dto.CustomerAddressId.Value);

                var addr = await _unitOfWork.CustomerAddresses.GetByIdAsync(dto.CustomerAddressId.Value);
                if (addr == null) throw new BadRequestException("CustomerAddressId inválido.");

                var targetCustomerId = dto.CustomerId ?? entity.CustomerId;
                if (targetCustomerId.HasValue && addr.CustomerId != targetCustomerId.Value)
                    throw new BadRequestException("CustomerAddressId não pertence ao CustomerId do feedback.");

                entity.CustomerAddressId = dto.CustomerAddressId.Value;
            }

            if (!string.IsNullOrWhiteSpace(dto.Category)) entity.Category = dto.Category;
            if (dto.Status.HasValue) entity.Status = dto.Status.Value;
            if (dto.Date.HasValue) entity.Date = dto.Date.Value;
            if (!string.IsNullOrWhiteSpace(dto.Description)) entity.Description = dto.Description;
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

            await EnsureCanAccessAsync(entity, forWrite: true);

            _unitOfWork.InternalFeedbacks.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<InternalFeedbackComment> AddCommentAsync(int feedbackId, CreateInternalFeedbackCommentDTO dto)
        {
            var feedback = await _unitOfWork.InternalFeedbacks.GetByIdAsync(feedbackId);
            if (feedback == null) throw new InvalidOperationException("Feedback not found");

            await EnsureCanAccessAsync(feedback, forWrite: true);

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
