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
        /// Papel de destino: por exemplo "Admin", "Professional", "Client".
        /// </summary>
        public string RecipientRole { get; set; } = null!;

        /// <summary>
        /// IDs de usuários que irão receber a notificação.
        /// Se IsBroadcast = true, esta lista pode ser ignorada.
        /// </summary>
        public List<int>? UserIds { get; set; }

        /// <summary>
        /// Se true, notificação é enviada para todos do papel RecipientRole.
        /// </summary>
        public bool IsBroadcast { get; set; }

        /// <summary>
        /// Opcional: ID do usuário relacionado (quem criou ou dono lógico).
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// Opcional: relacionar com um profissional específico.
        /// </summary>
        public int? ProfessionalId { get; set; }

        /// <summary>
        /// Opcional: quando enviado em nome de uma empresa.
        /// </summary>
        public int? CompanyId { get; set; }
    }
}
