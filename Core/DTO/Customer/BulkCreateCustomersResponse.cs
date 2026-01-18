using System.Collections.Generic;

namespace Core.DTO.Customer
{
    public class BulkCreateCustomersResponse
    {
        public int TotalRows { get; set; }
        public int CreatedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<BulkCreateCustomerErrorDTO> Errors { get; set; } = new();
    }

    public class BulkCreateCustomerErrorDTO
    {
        /// <summary>
        /// Índice 1-based da linha na planilha (desconsiderando o cabeçalho).
        /// </summary>
        public int RowNumber { get; set; }

        /// <summary>
        /// Nome do campo/coluna que falhou.
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Mensagem amigável do erro.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
