using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Enums.Notifications;
using Core.Enums.User;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;

namespace Services
{
    public interface INotificationService
    {
        Task<List<Notification>> GetAsync(NotificationFiltersDTO filters);
        Task<Notification?> GetByIdAsync(int id);
        Task<List<Notification>> CreateAsync(CreateNotificationDTO dto);
        Task<Notification?> UpdateAsync(int id, UpdateNotificationDTO dto);
        Task<bool> DeleteAsync(int id);
        Task<Notification?> MarkAsReadAsync(int id);
        Task<List<Notification>> GetUserNotificationsAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPushNotificationSender _pushSender;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public NotificationService(IUnitOfWork unitOfWork, IPushNotificationSender pushSender, ICurrentUser currentUser, IScopeGuard scope)
        {
            _unitOfWork = unitOfWork;
            _pushSender = pushSender;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<List<Notification>> GetAsync(NotificationFiltersDTO filters)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para listar notificações gerais.");

            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (companyId.HasValue) filters.CompanyId = companyId.Value;
            }

            return await _unitOfWork.Notifications.GetAsync(filters);
        }

        public async Task<Notification?> GetByIdAsync(int id)
        {
            var n = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (n == null) return null;

            if (!_currentUser.IsAdmin)
            {
                if (n.CompanyId.HasValue && n.CompanyId.Value > 0)
                    await _scope.EnsureCompanyAccessAsync(n.CompanyId.Value);

                if (_currentUser.IsProfessional)
                {
                    var companyId = await _scope.GetScopedCompanyIdAsync();
                    var isDirect = n.RecipientId == _currentUser.UserId;
                    var isBroadcast = n.RecipientId == 0 && (n.CompanyId == null || n.CompanyId == 0 || (companyId.HasValue && n.CompanyId == companyId.Value));
                    if (!isDirect && !isBroadcast)
                        throw new ForbiddenException("Você não tem permissão para acessar esta notificação.");
                }
            }

            return n;
        }

        public async Task<List<Notification>> CreateAsync(CreateNotificationDTO dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para criar notificações.");

            if (!_currentUser.IsAdmin)
            {
                var companyId = await _scope.GetScopedCompanyIdAsync();
                if (!companyId.HasValue) throw new ForbiddenException("Escopo de company inválido.");
                dto.CompanyId = companyId.Value;

                // Company não pode criar broadcast global
                if (dto.IsBroadcast == true && (dto.CompanyId == null || dto.CompanyId == 0))
                    dto.CompanyId = companyId.Value;
            }

            var created = new List<Notification>();

            var typeEnum = Enum.Parse<NotificationType>(dto.Type, ignoreCase: true);
            var roleEnum = Enum.Parse<UserRole>(dto.RecipientRole, ignoreCase: true);
            var defaultStatus = NotificationStatus.Unread;

            // CompanyId é opcional. Se não vier, deve ficar NULL (não "0"), para permitir broadcasts globais.
            var companyIdFinal = dto.CompanyId;

            if (dto.IsBroadcast)
            {
                var broadcast = new Notification
                {
                    Title = dto.Title,
                    Message = dto.Message,
                    Type = typeEnum,
                    RecipientId = 0,
                    RecipientRole = roleEnum,
                    CompanyId = companyIdFinal,
                    UserId = dto.UserId,
                    ProfessionalId = dto.ProfessionalId,
                    Status = defaultStatus,
                    SentAt = DateTime.UtcNow
                };

                _unitOfWork.Notifications.Add(broadcast);
                created.Add(broadcast);
            }
            else
            {
                var userIds = dto.UserIds ?? new List<int>();
                foreach (var uid in userIds)
                {
                    var n = new Notification
                    {
                        Title = dto.Title,
                        Message = dto.Message,
                        Type = typeEnum,
                        RecipientId = uid,
                        RecipientRole = roleEnum,
                        CompanyId = companyIdFinal,
                        UserId = dto.UserId,
                        ProfessionalId = dto.ProfessionalId,
                        Status = defaultStatus,
                        SentAt = DateTime.UtcNow
                    };

                    _unitOfWork.Notifications.Add(n);
                    created.Add(n);
                }
            }

            await _unitOfWork.SaveAsync();

            // Dispara push notification (Web Push) para os destinatários
            await _pushSender.TrySendForCreatedNotificationsAsync(created, dto);

            return created;
        }

        public async Task<Notification?> UpdateAsync(int id, UpdateNotificationDTO dto)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para editar notificações.");

            var entity = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (entity == null) return null;

            if (!_currentUser.IsAdmin && entity.CompanyId.HasValue && entity.CompanyId.Value > 0)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId.Value);

            if (!string.IsNullOrWhiteSpace(dto.Title))
                entity.Title = dto.Title;
            if (!string.IsNullOrWhiteSpace(dto.Message))
                entity.Message = dto.Message;
            if (!string.IsNullOrWhiteSpace(dto.Type))
                entity.Type = Enum.Parse<NotificationType>(dto.Type, true);
            if (!string.IsNullOrWhiteSpace(dto.Status))
                entity.Status = Enum.Parse<NotificationStatus>(dto.Status, true);
            if (dto.ReadAt.HasValue)
                entity.ReadAt = dto.ReadAt.Value;

            entity.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.Notifications.Update(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (_currentUser.IsProfessional)
                throw new ForbiddenException("Profissional não tem permissão para excluir notificações.");

            var entity = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (entity == null) return false;

            if (!_currentUser.IsAdmin && entity.CompanyId.HasValue && entity.CompanyId.Value > 0)
                await _scope.EnsureCompanyAccessAsync(entity.CompanyId.Value);

            _unitOfWork.Notifications.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<Notification?> MarkAsReadAsync(int id)
        {
            var entity = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (entity == null) return null;

            // scope check
            if (!_currentUser.IsAdmin)
            {
                if (entity.CompanyId.HasValue && entity.CompanyId.Value > 0)
                    await _scope.EnsureCompanyAccessAsync(entity.CompanyId.Value);

                if (_currentUser.IsProfessional)
                {
                    var companyId = await _scope.GetScopedCompanyIdAsync();
                    var isDirect = entity.RecipientId == _currentUser.UserId;
                    var isBroadcast = entity.RecipientId == 0 && (entity.CompanyId == null || entity.CompanyId == 0 || (companyId.HasValue && entity.CompanyId == companyId.Value));
                    if (!isDirect && !isBroadcast)
                        throw new ForbiddenException("Você não tem permissão para marcar esta notificação.");
                }
            }

            entity.Status = NotificationStatus.Read;
            entity.ReadAt = DateTime.UtcNow;
            entity.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Notifications.Update(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId)
        {
            // Retorna:
            // 1) notificações diretas do usuário (RecipientId = uid)
            // 2) notificações broadcast (RecipientId = 0) compatíveis com o papel/empresa do usuário
            var uid = int.Parse(userId);

            await _scope.EnsureUserSelfOrAdminAsync(uid);

            var user = await _unitOfWork.Users.GetById(uid);

            // Busca diretas
            var direct = await _unitOfWork.Notifications.GetAsync(new NotificationFiltersDTO { RecipientId = uid });

            if (user == null)
                return direct.OrderByDescending(n => n.SentAt).ToList();

            var broadcastsAllByRole = await _unitOfWork.Notifications.GetAsync(new NotificationFiltersDTO
            {
                RecipientId = 0,
                RecipientRole = user.Role
            });

            var broadcasts = broadcastsAllByRole
                .Where(n => n.CompanyId == null || n.CompanyId == 0 || (user.CompanyId.HasValue && n.CompanyId == user.CompanyId.Value))
                .ToList();

            return direct
                .Concat(broadcasts)
                .OrderByDescending(n => n.SentAt)
                .ToList();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            var uid = int.Parse(userId);

            await _scope.EnsureUserSelfOrAdminAsync(uid);

            var user = await _unitOfWork.Users.GetById(uid);

            var direct = await _unitOfWork.Notifications.GetAsync(new NotificationFiltersDTO { RecipientId = uid });
            var directCount = direct.Count(n => n.Status == NotificationStatus.Unread);

            if (user == null)
                return directCount;

            var broadcastsAllByRole = await _unitOfWork.Notifications.GetAsync(new NotificationFiltersDTO
            {
                RecipientId = 0,
                RecipientRole = user.Role
            });

            var broadcasts = broadcastsAllByRole
                .Where(n => n.CompanyId == null || n.CompanyId == 0 || (user.CompanyId.HasValue && n.CompanyId == user.CompanyId.Value))
                .ToList();

            var broadcastCount = broadcasts.Count(n => n.Status == NotificationStatus.Unread);
            return directCount + broadcastCount;
        }
    }
}
