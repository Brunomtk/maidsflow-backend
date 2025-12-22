namespace Core.DTO.PushSubscriptions
{
    public class PublicPushConfigDTO
    {
        public string VapidPublicKey { get; set; } = string.Empty;

        public bool Configured { get; set; }
    }
}
