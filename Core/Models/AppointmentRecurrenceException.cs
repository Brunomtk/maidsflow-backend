using Core.Enums.Appointment;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Models
{
    public class AppointmentRecurrenceException : BaseModel
    {
        public Guid SeriesId { get; set; }

        // Identify the original occurrence being overridden/cancelled
        public DateTime OccurrenceStart { get; set; }
        public DateTime OccurrenceEnd { get; set; }

        // If true, this occurrence is removed (delete "This")
        public bool IsCancelled { get; set; }

        // Optional overrides for edit "This"
        public string? OverrideTitle { get; set; }
        public string? OverrideAddress { get; set; }
        public string? OverrideNotes { get; set; }

        public DateTime? OverrideStart { get; set; }
        public DateTime? OverrideEnd { get; set; }

        public AppointmentStatus? OverrideStatus { get; set; }
        public AppointmentType? OverrideType { get; set; }

        // Optional override for ServiceType (Payroll)
        public int? OverrideServiceTypeId { get; set; }

        public int? OverrideCustomerAddressId { get; set; }

        [JsonIgnore]
        public string? OverrideProfessionalIdsData { get; set; }

        [NotMapped]
        [JsonPropertyName("overrideProfessionalIds")]
        public List<int> OverrideProfessionalIds
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OverrideProfessionalIdsData))
                    return new List<int>();

                try
                {
                    var list = JsonSerializer.Deserialize<List<int>>(OverrideProfessionalIdsData);
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
                    OverrideProfessionalIdsData = null;
                else
                    OverrideProfessionalIdsData = JsonSerializer.Serialize(value.Distinct().ToList());
            }
        }
    }
}
