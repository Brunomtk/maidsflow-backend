using System;

namespace Core.DTO.Guesty
{
    public class GuestyIntegrationStatusDTO
    {
        public bool HasToken { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        public bool HasCredentials { get; set; }
        public string? ApiType { get; set; }
    }
}
