using System.Collections.Generic;

namespace Core.DTO.Customer
{
    public class HouseNotesResponseDTO
    {
        public int AddressId { get; set; }
        public int CustomerId { get; set; }
        public string? AccessNotes { get; set; }
        public string? GateCode { get; set; }
        public bool? HasPets { get; set; }
        public string? PetNotes { get; set; }
        public string? RestrictionsNotes { get; set; }
        public string? PriorityNotes { get; set; }
        public List<string> PhotoKeys { get; set; } = new();
        public List<string> PhotoUrls { get; set; } = new();
    }
}
