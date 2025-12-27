namespace Core.DTO.User;

public class UpdateUserAvatarRequest
{
    /// <summary>
    /// The S3 key returned by the presign endpoint.
    /// </summary>
    public required string Key { get; set; }
}
