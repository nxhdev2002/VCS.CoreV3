using VCS.CoreV3.Domain.Abstractions;

namespace VCS.CoreV3.Domain.Entities;

public sealed class ApiKeyEntity : ICreation, IModification, IUserData
{
    public Guid Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;

    // IUserData
    public Guid UserId { get; set; }

    // ICreation
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }

    // IModification
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    public DateTime? ExpiredAt { get; set; }
    public bool IsRevoked { get; set; }
    public string Plan { get; set; } = "free";
    public int RateLimit { get; set; } = 1000;
}