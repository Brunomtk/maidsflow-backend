using System;

namespace Core.DTO.Notifications
{
    /// <summary>
    /// Dados para atualizar uma notificação existente.
    /// </summary>
    public class UpdateNotificationDTO
    {
        public string? Title { get; set; }
        public string? Message { get; set; }

        /// <summary>
        /// Opcionalmente atualizar o tipo.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Marcar status como "Unread" ou "Read".
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Data em que foi lida (para status Read).
        /// </summary>
        public DateTime? ReadAt { get; set; }
    }
}
