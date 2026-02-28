namespace Core.Options
{
    public class GpsTrackingOptions
    {
        public const string SectionName = "GpsTracking";

        /// <summary>
        /// Optional ingest API key for server-to-server ingestion.
        /// If set, unauthenticated requests must send header: X-GPS-INGEST-KEY
        /// </summary>
        public string? IngestApiKey { get; set; }

        /// <summary>Reject points too far in the future (minutes).</summary>
        public int MaxFutureMinutes { get; set; } = 10;

        /// <summary>Reject points too far in the past (hours).</summary>
        public int MaxPastHours { get; set; } = 48;

        /// <summary>Dedup window (seconds). If last point is within this window and close enough, ignore.</summary>
        public int DedupWindowSeconds { get; set; } = 10;

        /// <summary>Dedup distance (meters).</summary>
        public double DedupDistanceMeters { get; set; } = 5;

        /// <summary>Downsampling minimum time gap (seconds) when returning route points.</summary>
        public int DownsampleMinSeconds { get; set; } = 15;

        /// <summary>Downsampling minimum distance (meters) when returning route points.</summary>
        public double DownsampleMinMeters { get; set; } = 10;

        /// <summary>How many days to keep GPS points.</summary>
        public int RetentionDays { get; set; } = 90;

        /// <summary>Enable reverse geocoding when address is missing.</summary>
        public bool EnableReverseGeocoding { get; set; } = false;
    }
}
