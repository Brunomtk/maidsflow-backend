using System.ComponentModel.DataAnnotations;
namespace Core.DTO.Checklist
{
    public class CreateCustomerAreaDTO
    {
        [Required] public int CustomerId { get; set; }
        [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
    }
}
