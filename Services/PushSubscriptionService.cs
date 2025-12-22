using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.PushSubscriptions;
using Core.Models;
using Infrastructure.Repositories;

namespace Services
{
    public interface IPushSubscriptionService
    {
        Task<List<PushSubscription>> GetMySubscriptionsAsync(int userId);
        Task<PushSubscription> UpsertAsync(int userId, BrowserPushSubscriptionDTO dto);
        Task<bool> UnsubscribeAsync(int userId, string endpoint);
    }

    public class PushSubscriptionService : IPushSubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PushSubscriptionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<PushSubscription>> GetMySubscriptionsAsync(int userId)
        {
            return await _unitOfWork.PushSubscriptions.GetByUserIdAsync(userId);
        }

        public async Task<PushSubscription> UpsertAsync(int userId, BrowserPushSubscriptionDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Endpoint))
                throw new ArgumentException("Endpoint é obrigatório.");

            if (dto.Keys == null || string.IsNullOrWhiteSpace(dto.Keys.P256dh) || string.IsNullOrWhiteSpace(dto.Keys.Auth))
                throw new ArgumentException("Keys (p256dh/auth) são obrigatórias.");

            var existing = await _unitOfWork.PushSubscriptions.GetByUserIdAndEndpointAsync(userId, dto.Endpoint);

            if (existing == null)
            {
                var entity = new PushSubscription
                {
                    UserId = userId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.Keys.P256dh,
                    Auth = dto.Keys.Auth,
                    ExpirationTime = dto.ExpirationTime,
                    UserAgent = dto.UserAgent,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };

                await _unitOfWork.PushSubscriptions.Add(entity);
                await _unitOfWork.SaveAsync();
                return entity;
            }

            existing.P256dh = dto.Keys.P256dh;
            existing.Auth = dto.Keys.Auth;
            existing.ExpirationTime = dto.ExpirationTime;
            existing.UserAgent = dto.UserAgent;
            existing.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.PushSubscriptions.Update(existing);
            await _unitOfWork.SaveAsync();
            return existing;
        }

        public async Task<bool> UnsubscribeAsync(int userId, string endpoint)
        {
            var existing = await _unitOfWork.PushSubscriptions.GetByUserIdAndEndpointAsync(userId, endpoint);
            if (existing == null) return false;

            _unitOfWork.PushSubscriptions.Delete(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
    }
}
