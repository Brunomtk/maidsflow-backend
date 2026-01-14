using System.Collections.Generic;
using Core.DTO;

namespace Core.DTO.Plan
{
    /// <summary>
    /// Payload mínimo para atualização de um plano.
    /// Campos opcionais para manter o PUT simples (estilo PATCH).
    /// Obs: Status é atualizado no endpoint /status.
    /// </summary>
    public class UpdatePlanRequest
    {
        public string? Name { get; set; }
        public decimal? Price { get; set; }

        /// <summary>
        /// Duração do plano em meses (contada a partir da ativação).
        /// </summary>
        public int? Duration { get; set; }

        // Opcional: IDs do Stripe para mapeamento do plano (Product/Price)
        public string? StripeProductId { get; set; }
        public string? StripePriceId { get; set; }

        /// <summary>
        /// Lista de funcionalidades. Se vier null, mantém o valor atual.
        /// Envie lista vazia para limpar.
        /// </summary>
        public List<string>? Features { get; set; }

        /// <summary>
        /// Limites opcionais. Se vier null, mantém o valor atual.
        /// (Os campos internos são nullable, então pode limpar mandando null neles.)
        /// </summary>
        public LimitsDTO? Limits { get; set; }
    }
}
