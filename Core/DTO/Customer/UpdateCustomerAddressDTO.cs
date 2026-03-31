using System.Collections.Generic;

namespace Core.DTO.Customer
{
    public class UpdateCustomerAddressDTO
    {
        public string? Label { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Observations { get; set; }

        public decimal? Ticket { get; set; }
        public string? Frequency { get; set; }
        public string? PaymentMethod { get; set; }

        public string? HouseAccessNotes { get; set; }
        public string? HouseGateCode { get; set; }
        public bool? HouseHasPets { get; set; }
        public string? HousePetNotes { get; set; }
        public string? HouseRestrictionsNotes { get; set; }
        public string? HousePriorityNotes { get; set; }
        public List<string>? HousePhotoUrls { get; set; }
    }
}
