namespace VCS.CoreV3.Domain.Abstractions;

public interface ISoftDeletion
{
    bool IsDeleted { get; set; }
    DateTime? DeletionTime { get; set; }
    Guid? DeleterId { get; set; }
}
