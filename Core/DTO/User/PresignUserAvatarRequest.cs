namespace Core.DTO.User;

public class PresignUserAvatarRequest
{
    /// <summary>
    /// Original file name (optional, used only to guess extension).
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// MIME type sent by the browser (ex: image/png).
    /// </summary>
    public required string ContentType { get; set; }
}
