using System;
using Core.Enums;
using Core.Enums.User;

namespace Core.DTO.Cancellation
{
    public class CancellationDto
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int CompanyId { get; set; }
        public string Reason { get; set; } = null!;
        public int CancelledById { get; set; }
        public UserRole CancelledByRole { get; set; }
        public DateTime CancelledAt { get; set; }
        public RefundStatus? RefundStatus { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
