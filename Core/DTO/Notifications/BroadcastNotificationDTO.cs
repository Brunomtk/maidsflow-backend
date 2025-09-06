namespace Core.DTO.Notifications
{
    /// <summary>
    /// Dados para broadcast de notificação a todos de um papel.
    /// </summary>
    public class BroadcastNotificationDTO
    {
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string RecipientRole { get; set; } = null!;

        /// <summary>
        /// Opcional: quando enviado em nome de uma empresa.
        /// </summary>
        public int? CompanyId { get; set; }
    }
}
