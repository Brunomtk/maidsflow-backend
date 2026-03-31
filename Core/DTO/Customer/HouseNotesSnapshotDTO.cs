using System.Collections.Generic;

namespace Core.DTO.Customer
{
    public class HouseNotesSnapshotDTO
    {
        public int? CustomerAddressId { get; set; }
        public string? Label { get; set; }
        public string? AccessNotes { get; set; }
        public string? GateCode { get; set; }
        public bool? HasPets { get; set; }
        public string? PetNotes { get; set; }
        public string? RestrictionsNotes { get; set; }
        public string? PriorityNotes { get; set; }
        public List<string> PhotoUrls { get; set; } = new List<string>();
    }
}
