using System;

namespace VCS.CoreV3.Domain.Entities
{
    public sealed class ApiKeyEntity
    {
        public Guid Id { get; set; }
        public string KeyHash { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public bool IsRevoked { get; set; }
        public string Plan { get; set; } = "free";
        public int RateLimit { get; set; } = 1000;
        public DateTime UpdatedAt { get; set; }
    }
}