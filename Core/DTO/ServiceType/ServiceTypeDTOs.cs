namespace Core.DTO.ServiceType
{
    public class ServiceTypeDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateServiceTypeDTO
    {
        public string Name { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateServiceTypeDTO
    {
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
    }
}
