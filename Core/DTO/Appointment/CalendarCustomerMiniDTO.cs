namespace Core.DTO.Appointment
{
    /// <summary>
    /// Versão reduzida do Customer para uso no calendário.
    /// </summary>
    public class CalendarCustomerMiniDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Campos úteis para notificações (e-mail/SMS) e telas de detalhe.
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        // Notification preferences (default: enabled)
        public bool ReceiveSms { get; set; } = true;
        public bool ReceiveEmail { get; set; } = true;
    }
}
