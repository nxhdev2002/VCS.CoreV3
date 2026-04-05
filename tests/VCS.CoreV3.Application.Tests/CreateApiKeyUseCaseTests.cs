using Microsoft.Extensions.Logging.Abstractions;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application.Tests;

public sealed class CreateApiKeyUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidUserId_SetsCorrectUserIdOnCreatedEntity()
    {
        var repo = new CapturingApiKeyRepository();
        var sut = new CreateApiKeyUseCase(repo, NullLogger<CreateApiKeyUseCase>.Instance);
        var userId = Guid.NewGuid();

        var result = await sut.ExecuteAsync(userId);

        Assert.NotNull(repo.CapturedEntity);
        Assert.Equal(userId, repo.CapturedEntity.UserId);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUserId_StoresHashNotRawKey()
    {
        var repo = new CapturingApiKeyRepository();
        var sut = new CreateApiKeyUseCase(repo, NullLogger<CreateApiKeyUseCase>.Instance);

        var result = await sut.ExecuteAsync(Guid.NewGuid());

        Assert.NotNull(repo.CapturedEntity);
        Assert.NotEqual(result.RawKey, repo.CapturedEntity.KeyHash);
        Assert.NotEmpty(repo.CapturedEntity.KeyHash);
        Assert.NotEmpty(result.RawKey);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUserId_ReturnsFreeDefaults()
    {
        var repo = new CapturingApiKeyRepository();
        var sut = new CreateApiKeyUseCase(repo, NullLogger<CreateApiKeyUseCase>.Instance);

        var result = await sut.ExecuteAsync(Guid.NewGuid());

        Assert.Equal("free", result.Plan);
        Assert.Equal(ApiKeyDefaults.DefaultFreeRateLimit, result.RateLimit);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    private sealed class CapturingApiKeyRepository : IApiKeyRepository
    {
        public ApiKeyEntity? CapturedEntity { get; private set; }

        public Task CreateAsync(ApiKeyEntity entity, CancellationToken ct = default)
        {
            CapturedEntity = entity;
            return Task.CompletedTask;
        }

        public Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash) => Task.FromResult<ApiKeyEntity?>(null);
        public Task<bool> RevokeAsync(Guid id) => Task.FromResult(false);
        public Task<bool> UpdateRateLimitAsync(Guid id, int newRateLimit) => Task.FromResult(false);
    }
}
