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
        /// Papel do destinatário: "Admin", "Company", "Professional", "Customer", etc.
        /// </summary>
        public string RecipientRole { get; set; } = null!;

        /// <summary>
        /// IDs dos usuários que deverão receber a notificação
        /// (normalmente os IDs dos profissionais quando RecipientRole = "Professional").
        /// Ignorado quando IsBroadcast = true.
        /// </summary>
        public List<int>? UserIds { get; set; }

        /// <summary>
        /// Quando verdadeiro, envia para todos do papel indicado (RecipientRole), ignorando UserIds.
        /// </summary>
        public bool IsBroadcast { get; set; }

        /// <summary>
        /// Opcional: relacionar com um usuário específico (criador ou alvo lógico).
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
