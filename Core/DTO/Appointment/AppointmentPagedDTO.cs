using System.Collections.Generic;

namespace Core.DTO.Appointment
{
    public class AppointmentPagedDTO
    {
        public int PageCount { get; set; }
        public int TotalItems { get; set; }
        public List<AppointmentDTO> Data { get; set; } = new();
    }
}
