using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Core.Enums.Appointment;

namespace Core.DTO.Appointment
{
    /// <summary>
    /// Payload específico para edição de ocorrência por InstanceId.
    ///
    /// O front pode enviar em dois formatos:
    /// 1) Campos diretos: { title, address, start, end, notes, professionalIds, ... }
    /// 2) Campos "override": { overrideTitle, overrideAddress, overrideStart, overrideEnd, overrideNotes, overrideProfessionalIds, ... }
    ///
    /// Este DTO aceita ambos e converte para UpdateAppointmentDTO.
    /// </summary>
    public class UpdateRecurrenceInstanceDTO
    {
        [JsonPropertyName("scope")]
        public RecurrenceScope Scope { get; set; } = RecurrenceScope.This;

        // --- formato direto ---
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("start")]
        public DateTime? Start { get; set; }

        [JsonPropertyName("end")]
        public DateTime? End { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("status")]
        public AppointmentStatus? Status { get; set; }

        [JsonPropertyName("type")]
        public AppointmentType? Type { get; set; }

        [JsonPropertyName("professionalIds")]
        public List<int>? ProfessionalIds { get; set; }

        [JsonPropertyName("companyId")]
        public int? CompanyId { get; set; }

        [JsonPropertyName("customerId")]
        public int? CustomerId { get; set; }

        [JsonPropertyName("teamId")]
        public int? TeamId { get; set; }

        [JsonPropertyName("timeZoneId")]
        public string? TimeZoneId { get; set; }

        [JsonPropertyName("recurrenceRule")]
        public string? RecurrenceRule { get; set; }

        [JsonPropertyName("recurrenceEnd")]
        public DateTime? RecurrenceEnd { get; set; }

        [JsonPropertyName("occurrenceCount")]
        public int? OccurrenceCount { get; set; }

        // --- formato override (compatibilidade) ---
        [JsonPropertyName("overrideTitle")]
        public string? OverrideTitle { get; set; }

        [JsonPropertyName("overrideAddress")]
        public string? OverrideAddress { get; set; }

        [JsonPropertyName("overrideStart")]
        public DateTime? OverrideStart { get; set; }

        [JsonPropertyName("overrideEnd")]
        public DateTime? OverrideEnd { get; set; }

        [JsonPropertyName("overrideNotes")]
        public string? OverrideNotes { get; set; }

        [JsonPropertyName("overrideStatus")]
        public AppointmentStatus? OverrideStatus { get; set; }

        [JsonPropertyName("overrideType")]
        public AppointmentType? OverrideType { get; set; }

        [JsonPropertyName("overrideProfessionalIds")]
        public List<int>? OverrideProfessionalIds { get; set; }

        [JsonPropertyName("overrideTeamId")]
        public int? OverrideTeamId { get; set; }

        [JsonPropertyName("overrideCustomerId")]
        public int? OverrideCustomerId { get; set; }

        public UpdateAppointmentDTO ToUpdateAppointmentDTO()
        {
            return new UpdateAppointmentDTO
            {
                Scope = Scope,

                Title = Title ?? OverrideTitle,
                Address = Address ?? OverrideAddress,
                Notes = Notes ?? OverrideNotes,

                Start = Start ?? OverrideStart,
                End = End ?? OverrideEnd,

                Status = Status ?? OverrideStatus,
                Type = Type ?? OverrideType,

                ProfessionalIds = ProfessionalIds ?? OverrideProfessionalIds,

                CompanyId = CompanyId,
                CustomerId = CustomerId ?? OverrideCustomerId,
                TeamId = TeamId ?? OverrideTeamId,

                TimeZoneId = TimeZoneId,

                // Recorrência (se precisar atualizar regra/limites em Scope=All)
                RecurrenceRule = RecurrenceRule,
                RecurrenceEnd = RecurrenceEnd,
                OccurrenceCount = OccurrenceCount
            };
        }
    }
}
