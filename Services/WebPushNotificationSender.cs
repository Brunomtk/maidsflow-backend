using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Models;
using Infrastructure;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

namespace Services
{
    public interface IPushNotificationSender
    {
        Task TrySendForCreatedNotificationsAsync(List<Notification> created, CreateNotificationDTO dto);
    }

    public class WebPushNotificationSender : IPushNotificationSender
    {
        private readonly DbContextClass _db;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _config;
        private readonly ILogger<WebPushNotificationSender> _logger;

        public WebPushNotificationSender(DbContextClass db, IUnitOfWork unitOfWork, IConfiguration config, ILogger<WebPushNotificationSender> logger)
        {
            _db = db;
            _unitOfWork = unitOfWork;
            _config = config;
            _logger = logger;
        }

        public async Task TrySendForCreatedNotificationsAsync(List<Notification> created, CreateNotificationDTO dto)
        {
            var publicKey = _config["WebPush:PublicKey"];
            var privateKey = _config["WebPush:PrivateKey"];
            var subject = _config["WebPush:Subject"] ?? "mailto:admin@maidsflow.com";

            if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
            {
                _logger.LogWarning("WebPush não configurado (WebPush:PublicKey/PrivateKey). Push não será enviado.");
                return;
            }

            List<int> targetUserIds;

            if (dto.IsBroadcast)
            {
                var q = _db.Users.AsNoTracking().Where(u => u.Role == dto.RecipientRole);
                if (dto.CompanyId.HasValue && dto.CompanyId.Value > 0)
                {
                    q = q.Where(u => u.CompanyId == dto.CompanyId.Value);
                }

                targetUserIds = await q.Select(u => u.Id).ToListAsync();
            }
            else
            {
                targetUserIds = created.Select(n => n.RecipientId).Distinct().ToList();
            }

            if (targetUserIds.Count == 0) return;

            var subs = await _unitOfWork.PushSubscriptions.GetByUserIdsAsync(targetUserIds);
            if (subs.Count == 0) return;

            var client = new WebPushClient();
            var vapid = new VapidDetails(subject, publicKey, privateKey);

            foreach (var sub in subs)
            {
                sub.LastPushAttemptAtUtc = DateTime.UtcNow;
                sub.LastError = null;

                try
                {
                    var targetNotification = dto.IsBroadcast
                        ? created.FirstOrDefault()
                        : created.FirstOrDefault(n => n.RecipientId == sub.UserId) ?? created.FirstOrDefault();

                    var payload = JsonSerializer.Serialize(new
                    {
                        title = dto.Title,
                        message = dto.Message,
                        body = dto.Message,
                        url = ResolveUrl(sub, targetNotification, dto),
                        notificationId = targetNotification?.Id,
                        type = dto.Type,
                        tag = targetNotification != null ? $"notification-{targetNotification.Id}" : $"notification-user-{sub.UserId}",
                        data = new
                        {
                            notificationId = targetNotification?.Id,
                            endpoint = sub.Endpoint,
                            subscriptionId = sub.Id,
                            companyId = sub.CompanyId,
                            role = sub.UserRole
                        }
                    });

                    var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSub, payload, vapid);

                    sub.LastSuccessfulPushAtUtc = DateTime.UtcNow;
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
                    _logger.LogError(ex, "Erro ao enviar WebPush. UserId={UserId} SubscriptionId={SubscriptionId}", sub.UserId, sub.Id);
                }
                finally
                {
                    sub.UpdatedDate = DateTime.UtcNow;
                    _unitOfWork.PushSubscriptions.Update(sub);
                }
            }

            await _unitOfWork.SaveAsync();
        }

        private static string ResolveUrl(Core.Models.PushSubscription sub, Notification? notification, CreateNotificationDTO dto)
        {
            if (notification?.Id > 0)
            {
                return (sub.UserRole ?? dto.RecipientRole)?.ToLowerInvariant() switch
                {
                    "admin" => $"/admin/notifications?id={notification.Id}",
                    "company" => $"/company/notifications?id={notification.Id}",
                    "professional" => $"/professional/notifications?id={notification.Id}",
                    _ => $"/notifications?id={notification.Id}"
                };
            }

            return (sub.UserRole ?? dto.RecipientRole)?.ToLowerInvariant() switch
            {
                "admin" => "/admin/notifications",
                "company" => "/company/notifications",
                "professional" => "/professional/notifications",
                _ => "/notifications"
            };
        }
    }
}
