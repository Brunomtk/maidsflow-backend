namespace Core.Options
{
    public class GoogleMapsOptions
    {
        public const string SectionName = "GoogleMaps";

        public string? ApiKey { get; set; }

        /// <summary>
        /// Base URL for Geocoding API.
        /// </summary>
        public string GeocodingBaseUrl { get; set; } = "https://maps.googleapis.com/maps/api/geocode/json";
    }
}
