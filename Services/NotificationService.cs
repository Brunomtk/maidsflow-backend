using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Enums.Notifications;
using Core.Enums.User;
using Core.Models;
using Infrastructure.Repositories;

namespace Services
{
    public class NotificationService : INotificationService
    {
        private readonly Infrastructure.Repositories.IUnitOfWork _unitOfWork;
        private readonly IPushNotificationSender _pushSender;

        public NotificationService(Infrastructure.Repositories.IUnitOfWork unitOfWork, IPushNotificationSender pushSender)
        {
            _unitOfWork = unitOfWork;
            _pushSender = pushSender;
        }

        public Task<List<Notification>> GetAsync(NotificationFiltersDTO filters)
            => _unitOfWork.Notifications.GetAsync(filters);

        public Task<Notification?> GetByIdAsync(int id)
            => _unitOfWork.Notifications.GetByIdAsync(id);

        public async Task<List<Notification>> CreateAsync(CreateNotificationDTO dto)
        {
            var created = new List<Notification>();

            var typeEnum = Enum.Parse<NotificationType>(dto.Type, ignoreCase: true);
            var roleEnum = Enum.Parse<UserRole>(dto.RecipientRole, ignoreCase: true);
            var defaultStatus = NotificationStatus.Unread;

            // CompanyId é opcional. Se não vier, deve ficar NULL (não "0"),
            // para permitir broadcasts globais e filtros corretos.
            var companyId = dto.CompanyId;

            if (dto.IsBroadcast)
            {
                var broadcast = new Notification
                {
                    Title = dto.Title,
                    Message = dto.Message,
                    Type = typeEnum,
                    RecipientId = 0,
                    RecipientRole = roleEnum,
                    CompanyId = companyId,
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
                        CompanyId = companyId,
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
            var entity = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (entity == null) return null;

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
            var entity = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (entity == null) return false;
            _unitOfWork.Notifications.Delete(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<Notification?> MarkAsReadAsync(int id)
        {
            var entity = await _unitOfWork.Notifications.GetByIdAsync(id);
            if (entity == null) return null;

            entity.Status = NotificationStatus.Read;
            entity.ReadAt = DateTime.UtcNow;
            entity.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Notifications.Update(entity);
            await _unitOfWork.SaveAsync();
            return entity;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId)
        {
            // Esse endpoint precisa retornar:
            // 1) notificações diretas do usuário (RecipientId = uid)
            // 2) notificações broadcast (RecipientId = 0) compatíveis com o papel/empresa do usuário
            var uid = int.Parse(userId);

	            // IUserRepository/IGenericRepository expõe GetById (Task<User>), não GetByIdAsync.
	            // Mantemos como nullable para lidar com usuário inexistente.
	            Core.Models.User? user = await _unitOfWork.Users.GetById(uid);
            if (user == null)
            {
                // Mantém comportamento anterior: sem usuário, retorna apenas as diretas.
                var onlyDirect = await _unitOfWork.Notifications.GetAsync(new NotificationFiltersDTO { RecipientId = uid });
                return onlyDirect;
            }

            // Busca diretas
            var direct = await _unitOfWork.Notifications.GetAsync(new NotificationFiltersDTO { RecipientId = uid });

            // Busca broadcasts (RecipientId=0) para o papel do usuário.
            // Regra de empresa:
            // - broadcast global: CompanyId NULL ou 0
            // - broadcast por empresa: CompanyId == user.CompanyId
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
	            // IUserRepository/IGenericRepository expõe GetById (Task<User>), não GetByIdAsync.
	            Core.Models.User? user = await _unitOfWork.Users.GetById(uid);

            // Diretas não lidas
            var direct = await _unitOfWork.Notifications.GetAsync(new NotificationFiltersDTO { RecipientId = uid });
            var directCount = direct.Count(n => n.Status == NotificationStatus.Unread);

            if (user == null)
                return directCount;

            // Broadcasts não lidas (no modelo atual, broadcast é um registro só; o Status é o mesmo pra todo mundo)
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
}
