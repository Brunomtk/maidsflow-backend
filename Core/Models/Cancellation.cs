// Core/Models/Cancellation.cs
using System;
using Core.Enums;
using Core.Enums.User;

namespace Core.Models
{
    public class Cancellation : BaseModel
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
        public RefundStatus RefundStatus { get; set; }
        public string? Notes { get; set; }
    }
}
