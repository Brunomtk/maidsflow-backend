namespace Core.DTO.ServiceTypes
{
    public class ServiceTypeDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Description { get; set; }
    }

    public class CreateServiceTypeDTO
    {
        public int? CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateServiceTypeDTO
    {
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
        public string? Description { get; set; }
    }
}
