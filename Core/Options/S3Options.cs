namespace Core.Options;

public class S3Options
{
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";

    // Root prefix for checklist photos. Example: "Checklists/"
    public string ChecklistPrefix { get; set; } = "Checklists/";

    // Root prefix for company avatars. Example: "AvatarCompany/"
    public string CompanyAvatarPrefix { get; set; } = "AvatarCompany/";

    // Root prefix for user avatars. Example: "UserAvatar/"
    public string UserAvatarPrefix { get; set; } = "UserAvatar/";


    // Root prefix for house notes photos. Example: "HouseNotes/"
    public string HouseNotesPrefix { get; set; } = "HouseNotes/";

    // Optional explicit credentials. Prefer environment variables / IAM role.
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string? SessionToken { get; set; }

    public int UploadUrlExpiresMinutes { get; set; } = 10;
    public int DownloadUrlExpiresMinutes { get; set; } = 60;
}
