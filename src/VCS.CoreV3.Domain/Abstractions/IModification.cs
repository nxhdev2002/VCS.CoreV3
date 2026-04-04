namespace VCS.CoreV3.Domain.Abstractions;

public interface IModification
{
    DateTime? LastModificationTime { get; set; }
    Guid? LastModifierId { get; set; }
}
