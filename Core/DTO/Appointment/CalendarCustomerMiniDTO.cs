namespace Core.DTO.Appointment
{
    /// <summary>
    /// Versão reduzida do Customer para uso no calendário.
    /// </summary>
    public class CalendarCustomerMiniDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
