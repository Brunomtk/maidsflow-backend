namespace Core.DTO.User;

public class PresignUserAvatarResponse
{
    public required string UploadUrl { get; set; }
    public required string Key { get; set; }
    public required DateTime ExpiresAt { get; set; }
}
