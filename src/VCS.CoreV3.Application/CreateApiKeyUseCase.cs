using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application;

public sealed class CreateApiKeyUseCase : ICreateApiKeyUseCase
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ILogger<CreateApiKeyUseCase> _logger;

    public CreateApiKeyUseCase(IApiKeyRepository apiKeyRepository, ILogger<CreateApiKeyUseCase> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _logger = logger;
    }

    public async Task<CreateApiKeyResult> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rawKeyBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = Convert.ToHexString(rawKeyBytes).ToLowerInvariant();
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();

        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            KeyHash = keyHash,
            UserId = userId,
            Plan = "free",
            RateLimit = ApiKeyDefaults.DefaultFreeRateLimit
        };

        await _apiKeyRepository.CreateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "ApiKey created for userId={UserId} keyId={KeyId} plan={Plan} rateLimit={RateLimit}",
            userId,
            entity.Id,
            entity.Plan,
            entity.RateLimit);

        return new CreateApiKeyResult(entity.Id, rawKey, userId, entity.Plan, entity.RateLimit);
    }
}
