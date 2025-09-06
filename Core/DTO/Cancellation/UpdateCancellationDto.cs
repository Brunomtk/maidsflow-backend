// Core/DTO/Cancellation/UpdateCancellationDto.cs
using Core.Enums;

namespace Core.DTO.Cancellation
{
    public class UpdateCancellationDto
    {
        public string? Reason { get; set; }
        public RefundStatus? RefundStatus { get; set; }
        public string? Notes { get; set; }
    }
}
