using Core.Enums;

namespace Core.Models
{
    public class Leader : BaseModel
    {
        // Relacionamento obrigatório com User (FK)
        public int UserId { get; set; }
        public User? User { get; set; }  // Permitir nulo para evitar problemas no EF se não carregar

        public string Name { get; set; } = string.Empty;

        public string? Email { get; set; }
        public string? Phone { get; set; }

        public string Region { get; set; } = string.Empty;

        public StatusEnum Status { get; set; } = StatusEnum.Active; // Valor padrão para evitar nulidade
    }
}
