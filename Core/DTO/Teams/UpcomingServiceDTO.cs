using System;

namespace Core.DTO.Teams
{
    public class UpcomingServiceDTO
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Time { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}