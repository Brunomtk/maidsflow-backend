using Core.Enums;

namespace Core.DTO.Plan
{
    public class CreatePlanRequest
    {
        // Informações básicas do plano
        public required string Name { get; set; }
        public required decimal Price { get; set; }
        public required int Duration { get; set; } // Duração em meses

        // Funcionalidades inclusas no plano
        public required List<string> Features { get; set; } = new();

        // Status inicial do plano
        public required StatusEnum Status { get; set; }

        // Limites do plano (profissionais, clientes, etc.)
        public LimitsDTO Limits { get; set; } = new();
    }
}
