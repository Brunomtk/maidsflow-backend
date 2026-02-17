using System;
using System.Collections.Generic;

namespace Core.DTO.Billing
{
    public class StripeDatesSyncResultDTO
    {
        public int CompaniesProcessed { get; set; }
        public int SubscriptionsSynced { get; set; }
        public int CompaniesSkipped { get; set; }
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Quando a sincronização foi executada (UTC)
        /// </summary>
        public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
