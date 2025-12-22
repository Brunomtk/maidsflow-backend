using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Enums.User;
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
                var roleEnum = Enum.Parse<UserRole>(dto.RecipientRole, ignoreCase: true);

                var q = _db.Users.AsNoTracking().Where(u => u.Role == roleEnum.ToString());
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

            // URL padrão por papel (para abrir a tela correta ao clicar no push)
            static string GetNotificationsUrlByRole(string role)
            {
                return role?.ToLowerInvariant() switch
                {
                    "admin" => "/admin/notifications",
                    "company" => "/company/notifications",
                    "professional" => "/professional/notifications",
                    "customer" => "/customer/notifications",
                    _ => "/notifications"
                };
            }

            var baseUrl = GetNotificationsUrlByRole(dto.RecipientRole);

            foreach (var sub in subs)
            {
                try
                {
                    // Cada destinatário pode ter um NotificationId diferente (quando não for broadcast).
                    var notifId = dto.IsBroadcast
                        ? created.FirstOrDefault()?.Id
                        : created.FirstOrDefault(n => n.RecipientId == sub.UserId)?.Id ?? created.FirstOrDefault()?.Id;

                    var payload = JsonSerializer.Serialize(new
                    {
                        title = dto.Title,
                        message = dto.Message,
                        url = baseUrl,
                        notificationId = notifId
                    });

                    var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSub, payload, vapid);
                }
                catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Push subscription inválida (removendo). UserId={UserId}", sub.UserId);
                    var entity = await _unitOfWork.PushSubscriptions.GetByUserIdAndEndpointAsync(sub.UserId, sub.Endpoint);
                    if (entity != null)
                    {
                        _unitOfWork.PushSubscriptions.Delete(entity);
                        await _unitOfWork.SaveAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao enviar WebPush. UserId={UserId}", sub.UserId);
                }
            }
        }
    }
}
