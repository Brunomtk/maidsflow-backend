// Core/Models/Location.cs

// Core/Models/Location.cs

// Core/Models/Location.cs

// Core/Models/Location.cs
using Core.Enums;

namespace Core.Models
{
    public class Location
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; } = string.Empty;
        public double Accuracy { get; set; }
    }
}

