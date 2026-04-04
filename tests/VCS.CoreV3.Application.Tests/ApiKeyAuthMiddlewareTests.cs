using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VCS.CoreV3.Adapters.Web;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application.Tests;

public sealed class ApiKeyAuthMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsEntityInItems_WhenValidKeyProvided()
    {
        var entity = ValidEntity();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Api-Key"] = "my-secret-key";
        var sut = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(ctx, new StubApiKeyRepository(entity));

        Assert.Same(entity, ctx.Items[ApiKeyHttpContextKeys.ResolvedEntity]);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotSetEntity_WhenHeaderMissing()
    {
        var ctx = new DefaultHttpContext();
        var sut = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(ctx, new StubApiKeyRepository(ValidEntity()));

        Assert.False(ctx.Items.ContainsKey(ApiKeyHttpContextKeys.ResolvedEntity));
    }

    [Fact]
    public async Task InvokeAsync_DoesNotSetEntity_WhenKeyNotFoundInRepository()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Api-Key"] = "unknown-key";
        var sut = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(ctx, new StubApiKeyRepository(null));

        Assert.False(ctx.Items.ContainsKey(ApiKeyHttpContextKeys.ResolvedEntity));
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext_WhenHeaderPresent()
    {
        var nextCalled = false;
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Api-Key"] = "some-key";
        var sut = new ApiKeyAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, new StubApiKeyRepository(null));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext_WhenHeaderAbsent()
    {
        var nextCalled = false;
        var ctx = new DefaultHttpContext();
        var sut = new ApiKeyAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await sut.InvokeAsync(ctx, new StubApiKeyRepository(null));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PassesSha256HashToRepository()
    {
        var capturedHash = string.Empty;
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Api-Key"] = "test-key";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-key"))).ToLowerInvariant();
        var sut = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(ctx, new CapturingApiKeyRepository(h => capturedHash = h));

        Assert.Equal(expected, capturedHash);
    }

    private static ApiKeyEntity ValidEntity() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        KeyHash = "somehash",
        Plan = "free",
        RateLimit = 1000,
    };

    private sealed class StubApiKeyRepository(ApiKeyEntity? result) : IApiKeyRepository
    {
        public Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash) => Task.FromResult(result);
        public Task<bool> RevokeAsync(Guid id) => Task.FromResult(false);
        public Task<bool> UpdateRateLimitAsync(Guid id, int newRateLimit) => Task.FromResult(false);
    }

    private sealed class CapturingApiKeyRepository(Action<string> capture) : IApiKeyRepository
    {
        public Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash)
        {
            capture(keyHash);
            return Task.FromResult<ApiKeyEntity?>(null);
        }

        public Task<bool> RevokeAsync(Guid id) => Task.FromResult(false);
        public Task<bool> UpdateRateLimitAsync(Guid id, int newRateLimit) => Task.FromResult(false);
    }
}
