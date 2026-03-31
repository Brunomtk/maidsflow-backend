using System;

namespace Core.DTO.Issues
{
    public class PresignIssuePhotoUploadRequest
    {
        public int? IssueId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public class PresignIssuePhotoUploadResponse
    {
        public string Key { get; set; } = string.Empty;
        public string UploadUrl { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}
