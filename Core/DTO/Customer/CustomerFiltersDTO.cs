using Core.Enums;

namespace Core.DTO.Customer
{
    public class CustomerFiltersDTO
    {
        public int? CompanyId { get; set; }
        public string? Name { get; set; }
        public string? Ssn { get; set; }
        public StatusEnum? Status { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
