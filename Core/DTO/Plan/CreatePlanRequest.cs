using Core.DTO;

namespace Core.DTO.Plan
{
    /// <summary>
    /// Payload mínimo para criação de um plano.
    /// Mantém apenas o necessário, como nas outras entidades.
    /// </summary>
    public class CreatePlanRequest
    {
        public required string Name { get; set; }
        public required decimal Price { get; set; }

        /// <summary>
        /// Duração do plano em meses (contada a partir da ativação).
        /// </summary>
        public required int Duration { get; set; }

        // Opcional: IDs do Stripe para mapeamento do plano (Product/Price)
        public string? StripeProductId { get; set; }
        public string? StripePriceId { get; set; }

        /// <summary>
        /// Lista opcional de funcionalidades do plano.
        /// </summary>
        public List<string>? Features { get; set; }

        /// <summary>
        /// Limites opcionais do plano (profissionais, clientes, etc.).
        /// </summary>
        public LimitsDTO? Limits { get; set; }
    }
}
