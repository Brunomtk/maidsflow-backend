namespace Core.DTO.Company
{
    public class PresignCompanyAvatarResponse
    {
        public required string Key { get; set; }
        public required string UploadUrl { get; set; }
        public required string ExpiresAtUtc { get; set; }
        public string? DownloadUrl { get; set; }
    }
}
