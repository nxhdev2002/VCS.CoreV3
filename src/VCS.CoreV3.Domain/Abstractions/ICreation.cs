namespace VCS.CoreV3.Domain.Abstractions;

public interface ICreation
{
    DateTime CreationTime { get; set; }
    Guid? CreatorId { get; set; }
}
