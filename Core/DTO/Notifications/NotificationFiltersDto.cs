namespace Core.DTO.Notifications
{
    public class NotificationFiltersDTO
    {
        public string? Type { get; set; }
        public string? RecipientRole { get; set; }
        public string? Search { get; set; }

        // 🔽 Novos filtros por ID
        public int? RecipientId { get; set; }
        public int? CompanyId { get; set; }
    }
}
