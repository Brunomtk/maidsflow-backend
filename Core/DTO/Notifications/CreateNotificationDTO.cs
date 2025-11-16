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
        /// Papel do destinatário: "Admin", "Company", "Professional" ou "Customer".
        /// </summary>
        public string RecipientRole { get; set; } = null!;

        /// <summary>
        /// Se verdadeiro, envia como broadcast para todos do papel informado.
        /// </summary>
        public bool IsBroadcast { get; set; }

        /// <summary>
        /// Lista de IDs de usuário para envio direto (quando não for broadcast).
        /// </summary>
        public List<int>? UserIds { get; set; }

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
