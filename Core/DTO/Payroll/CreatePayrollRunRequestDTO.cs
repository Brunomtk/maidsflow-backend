using System;

namespace Core.DTO.Payroll
{
    public class CreatePayrollRunRequestDTO
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string? Notes { get; set; }

        /// <summary>
        /// Se true, permite criar o fechamento mesmo com MissingRule=true (continua Draft).
        /// Se false, a criação falha se houver regras faltando.
        /// </summary>
        public bool AllowMissingRules { get; set; } = true;
    }
}
