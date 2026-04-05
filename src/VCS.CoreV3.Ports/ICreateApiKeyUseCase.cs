namespace VCS.CoreV3.Ports;

public sealed record CreateApiKeyResult(Guid Id, string RawKey, Guid UserId, string Plan, int RateLimit);

public interface ICreateApiKeyUseCase
{
    Task<CreateApiKeyResult> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default);
}
