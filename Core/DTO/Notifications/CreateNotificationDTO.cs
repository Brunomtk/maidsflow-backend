using System.Collections.Generic;

namespace Core.DTO.Notifications
{
    /// <summary>
    /// Dados para criar uma nova notificação.
    /// </summary>
    public class CreateNotificationDTO
    {
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;

        /// <summary>
        /// Tipo de notificação: "Info", "Warning", "Error" ou "Success".
        /// </summary>
        public string Type { get; set; } = null!;

        /// <summary>
        /// Papel do destinatário: "admin", "company" ou "professional".
        /// </summary>
        public string RecipientRole { get; set; } = null!;

        /// <summary>
        /// Lista de IDs de usuários que devem receber (quando não for broadcast).
        /// </summary>
        public List<int>? RecipientIds { get; set; }

        /// <summary>
        /// Se verdadeiro, irá enviar para todos do papel indicado (broadcast).
        /// </summary>
        public bool IsBroadcast { get; set; }

        /// <summary>
        /// Opcional: quando enviado em nome de uma empresa.
        /// </summary>
        public int? CompanyId { get; set; }
    }
}
