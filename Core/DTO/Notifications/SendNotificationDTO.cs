using System.Collections.Generic;

namespace Core.DTO.Notifications
{
    /// <summary>
    /// Dados para enviar notificação a destinatários específicos.
    /// </summary>
    public class SendNotificationDTO
    {
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string RecipientRole { get; set; } = null!;
        public List<int> RecipientIds { get; set; } = new();

        /// <summary>
        /// Opcional: quando enviado em nome de uma empresa.
        /// </summary>
        public int? CompanyId { get; set; }
    }
}
