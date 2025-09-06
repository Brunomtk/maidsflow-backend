namespace Core.DTO.Plan
{
    public class PlanPagedDTO
    {
        public int PageCount { get; set; }
        public List<PlanDTO> Result { get; set; } = new();
    }
}
