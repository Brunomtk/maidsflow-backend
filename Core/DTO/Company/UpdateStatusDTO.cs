using Core.Enums;

namespace Core.DTO.Company
{
    public class UpdateStatusDTO
    {
        public required StatusEnum Status { get; set; }
    }
}