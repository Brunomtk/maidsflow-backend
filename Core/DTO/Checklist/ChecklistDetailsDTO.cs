using System;
using System.Collections.Generic;
using Core.Enums;

namespace Core.DTO.Checklist
{
    public class ChecklistDetailsDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public ChecklistStatus Status { get; set; }
        public string? ObservacoesGerais { get; set; }
        public DateTime CreatedDate { get; set; }
        public CustomerSummaryDTO Customer { get; set; } = new();
        public AppointmentSummaryDTO? Appointment { get; set; }
        public List<ChecklistDetailsItemDTO> Items { get; set; } = new();
        public List<ChecklistDetailsAreaDTO> Areas { get; set; } = new();
    }

    public class ChecklistDetailsItemDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int CustomerAreaId { get; set; }
        public string CustomerAreaName { get; set; } = string.Empty;
        public ChecklistItemStatus? Status { get; set; }
        public string? Observacoes { get; set; }
        public List<ChecklistDetailsPhotoDTO> Photos { get; set; } = new();
    }

    public class ChecklistDetailsPhotoDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Descricao { get; set; }
    }

    public class ChecklistDetailsAreaDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ChecklistDetailsItemDTO> Items { get; set; } = new();
    }

    public class CustomerSummaryDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AppointmentSummaryDTO
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
