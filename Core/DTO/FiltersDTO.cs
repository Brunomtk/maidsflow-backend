namespace Core.DTO
{
    public class FiltersDTO
    {
        public string? Name { get; set; }
        public int pageNumber { get; set; } = 1;
        public int pageSize { get; set; } = 10;
    }
}
