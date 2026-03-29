namespace Core.DTO.PushSubscriptions
{
    public class PushNotificationOpenedDTO
    {
        public string Endpoint { get; set; } = null!;
        public int? NotificationId { get; set; }
    }
}
