using System.ComponentModel.DataAnnotations;
namespace Core.DTO.Checklist
{
    public class UpdateCustomerAreaDTO
    {
        [Required] public int Id { get; set; }
        [MaxLength(120)] public string? Name { get; set; }
        public bool? Active { get; set; }
    }
}
