// Core/DTO/Cancellation/CreateCancellationDto.cs
using Core.Enums;
using Core.Enums.User;

namespace Core.DTO.Cancellation
{
    public class CreateCancellationDto
    {
        public int AppointmentId { get; set; }
        public int CustomerId { get; set; }
        public int CompanyId { get; set; }
        public string Reason { get; set; } = null!;
        public int CancelledById { get; set; }
        public UserRole CancelledByRole { get; set; }
        public RefundStatus? RefundStatus { get; set; }
        public string? Notes { get; set; }
    }
}
