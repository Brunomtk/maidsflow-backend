using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Enums.Notifications;
using Core.Enums.User;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly DbContextClass _dbContext;

        public NotificationRepository(DbContextClass dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Notification>> GetAsync(NotificationFiltersDTO filters)
        {
            var query = _dbContext.Notifications.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filters.Type)
                && Enum.TryParse<NotificationType>(filters.Type, true, out var parsedType))
            {
                query = query.Where(n => n.Type == parsedType);
            }

            if (!string.IsNullOrWhiteSpace(filters.RecipientRole)
                && Enum.TryParse<UserRole>(filters.RecipientRole, true, out var parsedRole))
            {
                query = query.Where(n => n.RecipientRole == parsedRole);
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var s = filters.Search.ToLower();
                query = query.Where(n =>
                    n.Title.ToLower().Contains(s) ||
                    n.Message.ToLower().Contains(s));
            }

            // Filtros adicionais por ID
            if (filters.RecipientId.HasValue)
            {
                query = query.Where(n => n.RecipientId == filters.RecipientId.Value);
            }

            if (filters.CompanyId.HasValue)
            {
                query = query.Where(n => n.CompanyId == filters.CompanyId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<Notification?> GetByIdAsync(int id)
            => await _dbContext.Notifications.FindAsync(id);

        public void Add(Notification entity)
            => _dbContext.Notifications.Add(entity);

        public void Update(Notification entity)
            => _dbContext.Notifications.Update(entity);

        public void Delete(Notification entity)
            => _dbContext.Notifications.Remove(entity);
    }

    public interface INotificationRepository
    {
        Task<List<Notification>> GetAsync(NotificationFiltersDTO filters);
        Task<Notification?> GetByIdAsync(int id);
        void Add(Notification entity);
        void Update(Notification entity);
        void Delete(Notification entity);
    }
}
