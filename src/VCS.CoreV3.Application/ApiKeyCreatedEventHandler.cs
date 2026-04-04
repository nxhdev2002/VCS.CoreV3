using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application;

public sealed class ApiKeyCreatedEventHandler : IIntegrationEventHandler<ApiKeyCreatedEvent>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ILogger<ApiKeyCreatedEventHandler> _logger;

    public ApiKeyCreatedEventHandler(IApiKeyRepository apiKeyRepository, ILogger<ApiKeyCreatedEventHandler> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _logger = logger;
    }

    public string EventType => EventTypes.ApiKeyCreated;

    public async Task HandleAsync(IntegrationEventEnvelope<ApiKeyCreatedEvent> envelope, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(envelope.Payload.UserId);

        var rawKeyBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = Convert.ToHexString(rawKeyBytes).ToLowerInvariant();
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();

        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            KeyHash = keyHash,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Plan = "free",
            RateLimit = ApiKeyDefaults.DefaultFreeRateLimit
        };

        await _apiKeyRepository.CreateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "ApiKey created for userId={UserId} keyId={KeyId} messageId={MessageId} correlationId={CorrelationId}",
            envelope.Payload.UserId,
            entity.Id,
            envelope.MessageId,
            envelope.CorrelationId);
    }
}
