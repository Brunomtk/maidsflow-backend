namespace Core.DTO.Company
{
    public class PresignCompanyAvatarRequest
    {
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
    }
}
