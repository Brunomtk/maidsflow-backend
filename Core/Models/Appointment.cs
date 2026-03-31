using Core.Enums.Appointment;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Models
{
    public class Appointment : BaseModel
    {
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        // Datas
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        // Relacionamentos
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? CustomerAddressId { get; set; }
        public CustomerAddress? CustomerAddress { get; set; }

        public int? TeamId { get; set; }
        public Team? Team { get; set; }


        // Lista de profissionais associada ao agendamento (quando criado com ProfessionalIds)
        // Armazenamos como JSON no banco em ProfessionalIdsData, e expomos como array no JSON da API.
        [JsonIgnore]
        public string? ProfessionalIdsData { get; set; }

        [NotMapped]
        [JsonPropertyName("professionalIds")]
        public List<int> ProfessionalIds
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ProfessionalIdsData))
                    return new List<int>();

                try
                {
                    var list = JsonSerializer.Deserialize<List<int>>(ProfessionalIdsData);
                    return list?.Distinct().ToList() ?? new List<int>();
                }
                catch
                {
                    return new List<int>();
                }
            }
            set
            {
                if (value == null || !value.Any())
                {
                    ProfessionalIdsData = null;
                }
                else
                {
                    ProfessionalIdsData = JsonSerializer.Serialize(
                        value.Distinct().ToList()
                    );
                }
            }
        }

        // Status e tipo
        public AppointmentStatus Status { get; set; }
        public AppointmentType Type { get; set; }

        // Payroll / classificação
        // Category substitui (gradualmente) o antigo "Type" no front — por enquanto mantemos ambos para compatibilidade.
        public string? Category { get; set; }

        public int? ServiceTypeId { get; set; }
        public ServiceType? ServiceType { get; set; }

        public string? Notes { get; set; }

        // Recurrence fields
        public string? TimeZoneId { get; set; } // e.g., "America/Sao_Paulo"
        public bool IsRecurring { get; set; }                 // part of a recurrence?
        public string? RecurrenceRule { get; set; }           // RRULE iCal
        public Guid? SeriesId { get; set; }                   // series identifier
        public DateTime? RecurrenceEnd { get; set; }          // series end (UTC)
        public int? OccurrenceCount { get; set; }             // COUNT
        public bool IsException { get; set; }                 // instance turned into exception?
        public DateTime? OriginalStart { get; set; }          // UTC
        public DateTime? OriginalEnd { get; set; }            // UTC

        // External integration linkage (optional)
        // Helps keep idempotency when creating appointments from integrations (e.g. Guesty reservations)
        public string? ExternalSource { get; set; }           // e.g. "guesty"
        public string? ExternalReservationId { get; set; }    // reservationId from external system
        public string? ExternalListingId { get; set; }        // listingId/propertyId from external system
        public string? ExternalStatus { get; set; }           // optional status snapshot (confirmed/cancelled/etc.)

        public string? HouseNotesSnapshotJson { get; set; }
    }
}