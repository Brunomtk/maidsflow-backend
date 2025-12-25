using System.ComponentModel.DataAnnotations;

namespace Core.DTO.Checklist;

public class PresignChecklistItemPhotoDTO
{
    [Required] public int ItemId { get; set; }
    [Required] public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
}

public class PresignChecklistItemPhotoResponseDTO
{
    public string Key { get; set; } = string.Empty;
    public string UploadUrl { get; set; } = string.Empty;
    public long ExpiresAtUnixSeconds { get; set; }
}
