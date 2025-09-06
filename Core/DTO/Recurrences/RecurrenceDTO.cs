using Core.Enums;
using Core.Enums.Recurrence;
using System;

namespace Core.DTO.Recurrences
{
    public class RecurrenceDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public string Address { get; set; }

        public int? TeamId { get; set; }
        public string? TeamName { get; set; }

        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }

        public FrequencyEnum Frequency { get; set; }
        public int? Day { get; set; }

        public TimeSpan Time { get; set; }
        public int Duration { get; set; }

        public RecurrenceStatusEnum Status { get; set; }
        public RecurrenceTypeEnum Type { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string? Notes { get; set; }

        public DateTime? LastExecution { get; set; }
        public DateTime? NextExecution { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
