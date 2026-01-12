using System;

namespace Core.DTO.Payroll
{
    public class UpdatePayrollRunRequestDTO
    {
        /// <summary>
        /// Novo início do período. Para alterar o período, envie PeriodStart e PeriodEnd.
        /// </summary>
        public DateTime? PeriodStart { get; set; }

        /// <summary>
        /// Novo fim do período. Para alterar o período, envie PeriodStart e PeriodEnd.
        /// </summary>
        public DateTime? PeriodEnd { get; set; }

        /// <summary>
        /// Observações do run.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Se true (padrão), recalcula os itens ao alterar o período.
        /// </summary>
        public bool? RecalculateItems { get; set; }

        /// <summary>
        /// Permite salvar mesmo quando há itens sem regra.
        /// </summary>
        public bool AllowMissingRules { get; set; } = false;
    }
}
