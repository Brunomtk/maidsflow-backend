namespace Core.Options
{
    public sealed class MonthlyReportEmailOptions
    {
        public const string SectionName = "Reports:MonthlyEmail";

        public bool Enabled { get; set; } = true;
        public int RunOnDayOfMonth { get; set; } = 1;
        public int RunHourUtc { get; set; } = 8;
        public int PollIntervalMinutes { get; set; } = 60;
        public string TriggeredByValue { get; set; } = "system-monthly";
    }
}
