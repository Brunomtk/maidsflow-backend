using System;

namespace Services.Storage
{
    /// <summary>
    /// Result of a presigned upload generation.
    /// </summary>
    public sealed record PresignedUploadResult(
        string Key,
        string UploadUrl,
        DateTimeOffset ExpiresAtUtc
    )
    {
        // Backward-compatible alias used by some controllers/DTO mappings.
        public DateTimeOffset ExpiresAt => ExpiresAtUtc;
    }
}
