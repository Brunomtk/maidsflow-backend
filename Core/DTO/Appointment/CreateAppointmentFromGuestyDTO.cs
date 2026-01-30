using System;
using System.Collections.Generic;
using Core.Enums.Appointment;

namespace Core.DTO.Appointment
{
    /// <summary>
    /// Payload used by the frontend Guesty calendar overlay to create (or upsert) an Appointment
    /// based on a Guesty reservation/block.
    ///
    /// Notes about time:
    /// - This project currently stores Start/End exactly as the client sends (no timezone conversion).
    /// - Send the local time that you want to see in the MaidsFlow calendar.
    /// </summary>
    public class CreateAppointmentFromGuestyDTO
    {
        // Optional for admins; non-admins are always scoped to their company.
        public int? CompanyId { get; set; }

        // Optional: if omitted, the backend can infer customer/customerAddress from GuestyListingId
        // (when that listingId already exists in CustomerAddresses as GuestyListingId).
        public int? CustomerId { get; set; }
        public int? CustomerAddressId { get; set; }

        // Guesty identifiers
        public string GuestyReservationId { get; set; } = string.Empty;
        public string? GuestyListingId { get; set; }
        public string? GuestyStatus { get; set; }

        // Appointment payload
        public string? Title { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }

        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public int? DurationMinutes { get; set; } // convenience: if End is null, we compute End = Start + DurationMinutes

        public AppointmentStatus? Status { get; set; }
        public AppointmentType? Type { get; set; }
        public string? Category { get; set; }
        public int? ServiceTypeId { get; set; }
        public int? TeamId { get; set; }
        public List<int>? ProfessionalIds { get; set; }

        public string? TimeZoneId { get; set; }

        // Special shortcut for the "create from checkout" button.
        // If Start is null, and CheckoutDate is provided, Start will be built from it.
        public string? CheckoutDate { get; set; } // yyyy-MM-dd
        public string? CheckoutTime { get; set; } // HH:mm
    }
}
