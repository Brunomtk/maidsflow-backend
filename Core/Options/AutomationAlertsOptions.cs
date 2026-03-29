namespace Core.Options
{
    public class AutomationAlertsOptions
    {
        public const string SectionName = "AutomationAlerts";
        public bool Enabled { get; set; } = true;
        public string DefaultRecipientEmail { get; set; } = string.Empty;
        public string DefaultRecipientName { get; set; } = "MaidsFlow Admin";
        public string SubjectPrefix { get; set; } = "[MaidsFlow Automation Failure]";
        public string WebhookSecret { get; set; } = string.Empty;
    }
}
