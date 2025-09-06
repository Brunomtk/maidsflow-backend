using System.Collections.Generic;
using System.Threading.Tasks;
using Core.DTO.Notifications;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace ControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationsController(INotificationService service)
        {
            _service = service;
        }

        // GET: api/notifications?type=&recipientRole=&search=&recipientId=&companyId=
        [HttpGet]
        public async Task<ActionResult<List<Notification>>> Get([FromQuery] NotificationFiltersDTO filters)
        {
            var notifications = await _service.GetAsync(filters);
            return Ok(notifications);
        }

        // GET: api/notifications/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Notification>> GetById(int id)
        {
            var notification = await _service.GetByIdAsync(id);
            if (notification == null) return NotFound();
            return Ok(notification);
        }

        // POST: api/notifications
        [HttpPost]
        public async Task<ActionResult<List<Notification>>> Create(CreateNotificationDTO dto)
        {
            var created = await _service.CreateAsync(dto);
            var firstId = created.Count > 0 ? created[0].Id : 0;
            return CreatedAtAction(nameof(GetById), new { id = firstId }, created);
        }

        // PUT: api/notifications/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<Notification>> Update(int id, UpdateNotificationDTO dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        // DELETE: api/notifications/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // POST: api/notifications/{id}/read
        [HttpPost("{id}/read")]
        public async Task<ActionResult<Notification>> MarkAsRead(int id)
        {
            var updated = await _service.MarkAsReadAsync(id);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        // GET: api/notifications/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<Notification>>> GetUserNotifications(string userId)
        {
            var list = await _service.GetUserNotificationsAsync(userId);
            return Ok(list);
        }

        // GET: api/notifications/user/{userId}/unread-count
        [HttpGet("user/{userId}/unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount(string userId)
        {
            var count = await _service.GetUnreadCountAsync(userId);
            return Ok(count);
        }
    }
}
