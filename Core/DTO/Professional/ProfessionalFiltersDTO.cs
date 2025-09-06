namespace Core.DTO.Professional
{
    public class ProfessionalFiltersDTO
    {
        public int? CompanyId { get; set; }
        public int? TeamId { get; set; }
        public int? LeaderId { get; set; } 
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
