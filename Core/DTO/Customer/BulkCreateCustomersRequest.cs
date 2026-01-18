using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Customer
{
    public class BulkCreateCustomersRequest
    {
        /// <summary>
        /// Linhas da planilha.
        /// </summary>
        [Required]
        public List<BulkCreateCustomerRowDTO> Rows { get; set; } = new();

        /// <summary>
        /// Quando true, apenas valida e retorna erros sem persistir.
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Apenas para Admin: permite informar explicitamente a CompanyId alvo.
        /// Para Company, o escopo (claim companyId) é usado.
        /// </summary>
        public int? CompanyId { get; set; }
    }
}
