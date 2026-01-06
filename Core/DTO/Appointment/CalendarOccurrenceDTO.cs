using System;
using System.Collections.Generic;
using Core.Enums.Appointment;

namespace Core.DTO.Appointment
{

    /// <summary>
    /// Item pronto para renderizar no calendário.
    /// - Eventos normais: AppointmentId preenchido e IsVirtualOccurrence=false
    /// - Ocorrências recorrentes expandidas: InstanceId preenchido e IsVirtualOccurrence=true
    /// </summary>
    public class CalendarOccurrenceDTO
    {
        public string Id { get; set; } = string.Empty; // AppointmentId (normal) ou InstanceId (recorrente)

        public bool IsVirtualOccurrence { get; set; }
        public bool IsRecurring { get; set; }

        // Normal event
        public int? AppointmentId { get; set; }

        // Recurring occurrence (virtual)
        public int? AnchorAppointmentId { get; set; }
        public Guid? SeriesId { get; set; }
        public string? InstanceId { get; set; }

        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public string? Title { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }

        // Conveniência: alguns consumidores (n8n/notificações) preferem campos no nível raiz.
        // Também permanecem disponíveis dentro de Customer.
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }

        // Conveniência: nome da empresa responsável pelo agendamento (útil para templates de e-mail/n8n).
        public string? CompanyName { get; set; }

        // Alguns bancos/instâncias legadas podem ter estes campos como nullable.
        // Para o calendário, é melhor preservar null do que forçar 0.
        public int? CompanyId { get; set; }
        public int? CustomerId { get; set; }
        public int? TeamId { get; set; }

        public AppointmentStatus Status { get; set; }
        public AppointmentType Type { get; set; }

        // Front geralmente espera estes objetos (mesmo que simplificados) para exibir nome.
        public CalendarCustomerMiniDTO? Customer { get; set; }
        public CalendarTeamMiniDTO? Team { get; set; }

        // Alguns clientes ainda usam um "professionalId" único.
        public int? ProfessionalId { get; set; }

        public List<int> ProfessionalIds { get; set; } = new();

        // Helpful flags
        public bool IsCancelled { get; set; }
        public bool HasOverride { get; set; }
    }
}
