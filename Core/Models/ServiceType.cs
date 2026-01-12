namespace Core.Models
{
    /// <summary>
    /// Tipo de serviço configurável por Company (ex.: "Deep Clean", "Standard Clean").
    /// Usado principalmente no Payroll para definir rate/valores.
    /// </summary>
    public class ServiceType : BaseModel
    {
        public string Name { get; set; } = string.Empty;

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public bool IsActive { get; set; } = true;
    }
}
