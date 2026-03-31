using System;

namespace Core.DTO.Customer
{
    public class PresignHouseNotesPhotoUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public class PresignHouseNotesPhotoUploadResponse
    {
        public string Key { get; set; } = string.Empty;
        public string UploadUrl { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}
