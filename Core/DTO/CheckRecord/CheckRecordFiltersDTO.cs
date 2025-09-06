using System;

namespace Core.DTO.CheckRecord
{
    public class CheckRecordFiltersDTO
    {
        // Identificadores
        public int? ProfessionalId { get; set; }
        public int? CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? TeamId { get; set; }
        public int? AppointmentId { get; set; }

        // Filtros por status e tipo de serviço
        public string? Status { get; set; }         // Enum: CheckRecordStatus
        public string? ServiceType { get; set; }    // Ex: "Instalação", "Manutenção"

        // Intervalo de datas (string format "yyyy-MM-dd" esperado)
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }

        // Busca geral (por nome, endereço, observação etc.)
        public string? Search { get; set; }

        // Paginação
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
