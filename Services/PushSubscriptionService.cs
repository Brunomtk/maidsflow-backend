using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Core.DTO.PushSubscriptions;
using Core.Models;
using Infrastructure.Repositories;
using WebPush;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services
{
    public class PushSubscriptionService : IPushSubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _config;
        private readonly ILogger<PushSubscriptionService> _logger;

        public PushSubscriptionService(IUnitOfWork unitOfWork, IConfiguration config, ILogger<PushSubscriptionService> logger)
        {
            _unitOfWork = unitOfWork;
            _config = config;
            _logger = logger;
        }

        public async Task<List<PushSubscriptionSummaryDTO>> GetMySubscriptionsAsync(int userId)
        {
            var entities = await _unitOfWork.PushSubscriptions.GetByUserIdAsync(userId);
            return entities.Select(MapSummary).ToList();
        }

        public async Task<PushSubscriptionSummaryDTO> UpsertAsync(int userId, BrowserPushSubscriptionDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Endpoint))
                throw new ArgumentException("Endpoint é obrigatório.");

            if (dto.Keys == null || string.IsNullOrWhiteSpace(dto.Keys.P256dh) || string.IsNullOrWhiteSpace(dto.Keys.Auth))
                throw new ArgumentException("Keys (p256dh/auth) são obrigatórias.");

            var user = await _unitOfWork.Users.GetById(userId);
            var now = DateTime.UtcNow;
            var existing = await _unitOfWork.PushSubscriptions.GetByUserIdAndEndpointAsync(userId, dto.Endpoint);
            var isNew = existing == null;

            if (existing == null)
            {
                existing = new Core.Models.PushSubscription
                {
                    UserId = userId,
                    CompanyId = user?.CompanyId,
                    UserRole = user?.Role,
                    Endpoint = dto.Endpoint,
                    CreatedDate = now
                };

                await _unitOfWork.PushSubscriptions.Add(existing);
            }

            existing.P256dh = dto.Keys.P256dh;
            existing.Auth = dto.Keys.Auth;
            existing.ExpirationTime = dto.ExpirationTime;
            existing.UserAgent = NullIfWhiteSpace(dto.UserAgent);
            existing.DeviceId = NullIfWhiteSpace(dto.DeviceId);
            existing.DeviceName = NullIfWhiteSpace(dto.DeviceName);
            existing.Platform = NormalizeShortText(dto.Platform);
            existing.BrowserName = NormalizeShortText(dto.BrowserName);
            existing.IsPwaInstalled = dto.IsPwaInstalled ?? existing.IsPwaInstalled;
            existing.PermissionState = NormalizeShortText(dto.PermissionState);
            existing.CompanyId = user?.CompanyId;
            existing.UserRole = user?.Role;
            existing.IsActive = true;
            existing.LastSeenAtUtc = now;
            existing.LastError = null;
            existing.UpdatedDate = now;

            if (!isNew)
                _unitOfWork.PushSubscriptions.Update(existing);

            await _unitOfWork.SaveAsync();
            return MapSummary(existing);
        }

        public async Task<bool> UnsubscribeAsync(int userId, string endpoint)
        {
            var existing = await _unitOfWork.PushSubscriptions.GetByUserIdAndEndpointAsync(userId, endpoint);
            if (existing == null) return false;

            _unitOfWork.PushSubscriptions.Delete(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<bool> MarkOpenedAsync(int userId, PushNotificationOpenedDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Endpoint)) return false;

            var existing = await _unitOfWork.PushSubscriptions.GetByUserIdAndEndpointAsync(userId, dto.Endpoint);
            if (existing == null) return false;

            existing.LastPushOpenedAtUtc = DateTime.UtcNow;
            existing.LastSeenAtUtc = DateTime.UtcNow;
            existing.LastError = null;
            existing.IsActive = true;
            _unitOfWork.PushSubscriptions.Update(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<PushSubscriptionSummaryDTO?> SendTestAsync(int userId, PushNotificationTestDTO dto)
        {
            var target = dto.SubscriptionId.HasValue
                ? await _unitOfWork.PushSubscriptions.GetByUserIdAndSubscriptionIdAsync(userId, dto.SubscriptionId.Value)
                : !string.IsNullOrWhiteSpace(dto.Endpoint)
                    ? await _unitOfWork.PushSubscriptions.GetByUserIdAndEndpointAsync(userId, dto.Endpoint)
                    : (await _unitOfWork.PushSubscriptions.GetByUserIdAsync(userId)).FirstOrDefault(s => s.IsActive);

            if (target == null) return null;

            await SendPushToSubscriptionAsync(
                target,
                dto.Title ?? "MaidsFlow test notification",
                dto.Message ?? "If you received this push, the background delivery is working.",
                dto.Url ?? ResolveDefaultUrl(target.UserRole),
                notificationId: null,
                type: "test");

            return MapSummary(target);
        }

        internal async Task SendPushToSubscriptionAsync(
            Core.Models.PushSubscription sub,
            string title,
            string message,
            string url,
            int? notificationId,
            string type)
        {
            var publicKey = _config["WebPush:PublicKey"];
            var privateKey = _config["WebPush:PrivateKey"];
            var subject = _config["WebPush:Subject"] ?? "mailto:admin@maidsflow.com";

            if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
                throw new InvalidOperationException("WebPush não configurado (WebPush:PublicKey/PrivateKey).");

            var payload = JsonSerializer.Serialize(new
            {
                title,
                message,
                body = message,
                url,
                type,
                notificationId,
                tag = notificationId.HasValue ? $"notification-{notificationId.Value}" : $"test-{sub.Id}",
                data = new
                {
                    notificationId,
                    subscriptionId = sub.Id,
                    endpoint = sub.Endpoint,
                    url,
                    type
                }
            });

            var client = new WebPushClient();
            var vapid = new VapidDetails(subject, publicKey, privateKey);

            sub.LastPushAttemptAtUtc = DateTime.UtcNow;
            sub.LastError = null;

            try
            {
                var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapid);
                sub.LastSuccessfulPushAtUtc = DateTime.UtcNow;
                sub.LastSeenAtUtc = DateTime.UtcNow;
                sub.LastError = null;
                sub.FailureCount = 0;
                sub.IsActive = true;
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                sub.IsActive = false;
                sub.FailureCount += 1;
                sub.LastError = $"Subscription inválida: {(int)ex.StatusCode} {ex.StatusCode}";
                _logger.LogInformation("Push subscription inválida. UserId={UserId} SubscriptionId={SubscriptionId}", sub.UserId, sub.Id);
            }
            catch (Exception ex)
            {
                sub.FailureCount += 1;
                sub.LastError = ex.Message;
                _logger.LogError(ex, "Erro ao enviar push de teste. UserId={UserId} SubscriptionId={SubscriptionId}", sub.UserId, sub.Id);
                throw;
            }
            finally
            {
                sub.UpdatedDate = DateTime.UtcNow;
                _unitOfWork.PushSubscriptions.Update(sub);
                await _unitOfWork.SaveAsync();
            }
        }

        private static PushSubscriptionSummaryDTO MapSummary(Core.Models.PushSubscription entity)
        {
            return new PushSubscriptionSummaryDTO
            {
                Id = entity.Id,
                Endpoint = entity.Endpoint,
                DeviceId = entity.DeviceId,
                DeviceName = entity.DeviceName,
                Platform = entity.Platform,
                BrowserName = entity.BrowserName,
                IsPwaInstalled = entity.IsPwaInstalled,
                PermissionState = entity.PermissionState,
                IsActive = entity.IsActive,
                FailureCount = entity.FailureCount,
                LastSeenAtUtc = entity.LastSeenAtUtc,
                LastPushAttemptAtUtc = entity.LastPushAttemptAtUtc,
                LastSuccessfulPushAtUtc = entity.LastSuccessfulPushAtUtc,
                LastPushOpenedAtUtc = entity.LastPushOpenedAtUtc,
                LastError = entity.LastError,
                UpdatedDate = entity.UpdatedDate
            };
        }

        private static string ResolveDefaultUrl(string? role)
        {
            return role?.ToLowerInvariant() switch
            {
                "admin" => "/admin/notifications",
                "company" => "/company/notifications",
                "professional" => "/professional/notifications",
                _ => "/notifications"
            };
        }

        private static string? NormalizeShortText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length <= 100 ? value : value[..100];
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
