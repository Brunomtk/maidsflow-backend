namespace Core.DTO.Appointment
{
    /// <summary>
    /// Versão reduzida do Team para uso no calendário.
    /// </summary>
    public class CalendarTeamMiniDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
