using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

        [Phone]
        public string? Phone { get; set; }

        [Phone]
        public string? Phone2 { get; set; }

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


        [JsonPropertyName("accessNotes")]
        public string? AccessNotes { set => HouseAccessNotes = value; }

        [JsonPropertyName("gateCode")]
        public string? GateCode { set => HouseGateCode = value; }

        [JsonPropertyName("hasPets")]
        public bool? HasPets { set => HouseHasPets = value; }

        [JsonPropertyName("petNotes")]
        public string? PetNotes { set => HousePetNotes = value; }

        [JsonPropertyName("restrictionsNotes")]
        public string? RestrictionsNotes { set => HouseRestrictionsNotes = value; }

        [JsonPropertyName("priorityNotes")]
        public string? PriorityNotes { set => HousePriorityNotes = value; }

        [JsonPropertyName("photoUrls")]
        public List<string>? PhotoUrls { set => HousePhotoUrls = value; }
    }
}
