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
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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

            var companyId = dto.CompanyId ?? 0;

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
                    Status = defaultStatus,
                    SentAt = DateTime.UtcNow
                };
                _unitOfWork.Notifications.Add(broadcast);
                created.Add(broadcast);
            }
            else
            {
                var recipientIds = dto.RecipientIds ?? new List<int>();
                foreach (var rid in recipientIds)
                {
                    var n = new Notification
                    {
                        Title = dto.Title,
                        Message = dto.Message,
                        Type = typeEnum,
                        RecipientId = rid,
                        RecipientRole = roleEnum,
                        CompanyId = companyId,
                        Status = defaultStatus,
                        SentAt = DateTime.UtcNow
                    };
                    _unitOfWork.Notifications.Add(n);
                    created.Add(n);
                }
            }

            await _unitOfWork.SaveAsync();
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
            var uid = int.Parse(userId);
            var filters = new NotificationFiltersDTO { RecipientId = uid };
            var all = await _unitOfWork.Notifications.GetAsync(filters);

            return all
                .Where(n => n.RecipientId == uid || n.RecipientId == 0)
                .ToList();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            var uid = int.Parse(userId);
            var filters = new NotificationFiltersDTO { RecipientId = uid };
            var all = await _unitOfWork.Notifications.GetAsync(filters);
            return all.Count(n => n.RecipientId == uid && n.Status == NotificationStatus.Unread);
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
