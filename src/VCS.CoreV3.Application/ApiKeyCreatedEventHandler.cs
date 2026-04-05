using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application;

public sealed class ApiKeyCreatedEventHandler : IIntegrationEventHandler<ApiKeyCreatedEvent>
{
    private readonly ICreateApiKeyUseCase _createApiKeyUseCase;

    public ApiKeyCreatedEventHandler(ICreateApiKeyUseCase createApiKeyUseCase)
    {
        _createApiKeyUseCase = createApiKeyUseCase;
    }

    public string EventType => EventTypes.ApiKeyCreated;

    public async Task HandleAsync(IntegrationEventEnvelope<ApiKeyCreatedEvent> envelope, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(envelope.Payload.UserId);
        await _createApiKeyUseCase.ExecuteAsync(userId, cancellationToken);
    }
}
