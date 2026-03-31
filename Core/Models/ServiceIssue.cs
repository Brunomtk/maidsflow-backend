using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace Core.Models
{
    public class ServiceIssue : BaseModel
    {
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? CustomerAddressId { get; set; }
        public CustomerAddress? CustomerAddress { get; set; }

        public int? ProfessionalId { get; set; }
        public Professional? Professional { get; set; }

        public int ReportedByUserId { get; set; }
        public User? ReportedByUser { get; set; }

        public int? ReviewedByUserId { get; set; }
        public User? ReviewedByUser { get; set; }

        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = "open";
        public string Summary { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? InternalNotes { get; set; }

        public decimal? EstimatedAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }

        public string? PhotoUrlsJson { get; set; }
        public System.DateTime? ResolvedAtUtc { get; set; }

        [NotMapped]
        public List<string> PhotoUrls
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PhotoUrlsJson))
                    return new List<string>();

                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(PhotoUrlsJson);
                    return list?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                if (value == null || value.Count == 0)
                {
                    PhotoUrlsJson = null;
                    return;
                }

                PhotoUrlsJson = JsonSerializer.Serialize(
                    value.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList()
                );
            }
        }
    }
}
