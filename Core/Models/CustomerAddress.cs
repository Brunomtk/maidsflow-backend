using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace Core.Models
{
    public class CustomerAddress : BaseModel
    {
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public string? Label { get; set; }

        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }

        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? ZipCode { get; set; }

        /// <summary>
        /// Optional primary phone number for this specific address.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Optional second phone number for this specific address. The address is limited to Phone and Phone2.
        /// </summary>
        public string? Phone2 { get; set; }

        public string? Observations { get; set; }

        public decimal? Ticket { get; set; }
        public string? Frequency { get; set; }
        public string? PaymentMethod { get; set; }

        // Guesty integration (optional): when present, this address was created/synced from a Guesty listing.
        public string? GuestyListingId { get; set; }
        public string? GuestyListingTitle { get; set; }
        public DateTime? GuestySyncedAtUtc { get; set; }

        public bool IsPrimary { get; set; }

        public string? HouseAccessNotes { get; set; }
        public string? HouseGateCode { get; set; }
        public bool? HouseHasPets { get; set; }
        public string? HousePetNotes { get; set; }
        public string? HouseRestrictionsNotes { get; set; }
        public string? HousePriorityNotes { get; set; }
        public string? HousePhotoUrlsJson { get; set; }

        [NotMapped]
        public List<string> HousePhotoUrls
        {
            get
            {
                if (string.IsNullOrWhiteSpace(HousePhotoUrlsJson))
                    return new List<string>();

                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(HousePhotoUrlsJson);
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
                    HousePhotoUrlsJson = null;
                    return;
                }

                HousePhotoUrlsJson = JsonSerializer.Serialize(
                    value.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList()
                );
            }
        }

        public ICollection<CustomerArea> Areas { get; set; } = new List<CustomerArea>();
        public ICollection<Checklist> Checklists { get; set; } = new List<Checklist>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
