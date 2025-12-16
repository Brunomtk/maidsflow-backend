using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Plan
{
    public class ActivatePlanDTO
    {
        [Required]
        public int CompanyId { get; set; }

        /// <summary>
        /// Se true, quando vencer o plano pode ser renovado automaticamente (se você implementar a cobrança/renovação).
        /// </summary>
        public bool AutoRenew { get; set; } = false;
    }
}
