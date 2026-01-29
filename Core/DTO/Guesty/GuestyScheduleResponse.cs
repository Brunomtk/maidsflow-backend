using System.Collections.Generic;

namespace Core.DTO.Guesty
{
    public class GuestyScheduleResponse
    {
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;

        public List<GuestyListingDTO> Listings { get; set; } = new();
        public List<GuestyScheduleEventDTO> Events { get; set; } = new();
    }
}
