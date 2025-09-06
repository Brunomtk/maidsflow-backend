// Core/DTO/Cancellation/ProcessRefundDto.cs
using Core.Enums;

namespace Core.DTO.Cancellation
{
    public class ProcessRefundDto
    {
        public RefundStatus Status { get; set; }
        public string? Notes { get; set; }
    }
}
