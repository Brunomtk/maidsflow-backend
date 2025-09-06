namespace Core.DTO.Teams
{
    public class TeamFiltersDTO
    {
        public int? CompanyId { get; set; }
        public int? LeaderId { get; set; }
        public string? Status { get; set; } = "all";
        public string? Search { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
