using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Models
{
    /// <summary>
    /// Snapshot of a completed appointment occurrence.
    /// This is critical for recurring appointments (1 anchor row + exceptions),
    /// because payroll needs a stable "occurrence id" to compute payments reliably.
    /// </summary>
    public class AppointmentCompletion : BaseModel
    {
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>
        /// Anchor appointment id (Appointments table).
        /// </summary>
        public int AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        public Guid? SeriesId { get; set; }

        /// <summary>
        /// Occurrence window (stored as local/unspecified like the rest of the project).
        /// </summary>
        public DateTime OccurrenceStart { get; set; }
        public DateTime OccurrenceEnd { get; set; }

        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        public int? CustomerIdSnapshot { get; set; }
        public int? CustomerAddressIdSnapshot { get; set; }
        public int? TeamIdSnapshot { get; set; }

        public string? CategorySnapshot { get; set; }
        public int? ServiceTypeIdSnapshot { get; set; }

        /// <summary>
        /// Base amount used for payroll calculations (Percent). For now we snapshot Customer.Ticket.
        /// </summary>
        public decimal SourceAmountSnapshot { get; set; }

        public string? CustomerAddressSnapshot { get; set; }
        public string? PaymentMethodSnapshot { get; set; }
        public string? FrequencySnapshot { get; set; }

        /// <summary>
        /// JSON array of professional ids that actually worked on this occurrence.
        /// Always try to snapshot this to avoid later team membership changes affecting history.
        /// </summary>
        public string? ProfessionalIdsDataSnapshot { get; set; }

        [NotMapped]
        [JsonIgnore]
        public List<int> ProfessionalIdsSnapshot
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ProfessionalIdsDataSnapshot)) return new List<int>();
                try
                {
                    var ids = JsonSerializer.Deserialize<List<int>>(ProfessionalIdsDataSnapshot);
                    return ids ?? new List<int>();
                }
                catch
                {
                    return new List<int>();
                }
            }
            set
            {
                ProfessionalIdsDataSnapshot = value == null ? null : JsonSerializer.Serialize(value);
            }
        }
    }
}
