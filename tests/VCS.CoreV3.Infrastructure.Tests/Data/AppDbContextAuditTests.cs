#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Infrastructure.Data;
using VCS.CoreV3.Ports;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests.Data;

public sealed class AppDbContextAuditTests
{
    private static AppDbContext CreateContext(ICurrentUser? currentUser = null, TimeProvider? timeProvider = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options, currentUser ?? new NullCurrentUser(), timeProvider ?? TimeProvider.System);
    }

    [Fact]
    public async Task SaveChangesAsync_AddedICreationEntity_SetsCreatedAtUtc()
    {
        var fakeUser = new FakeCurrentUser(Guid.NewGuid());
        using var db = CreateContext(fakeUser);

        var entity = NewApiKey();
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        Assert.NotEqual(default, entity.CreationTime);
    }

    [Fact]
    public async Task SaveChangesAsync_AddedICreationEntity_SetsCreatedByToCurrentUserId()
    {
        var userId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUser(userId);
        using var db = CreateContext(fakeUser);

        var entity = NewApiKey();
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        Assert.Equal(userId, entity.CreatorId);
    }

    [Fact]
    public async Task SaveChangesAsync_AddedEntity_UnauthenticatedUser_SetsCreatedByToNull()
    {
        using var db = CreateContext(new NullCurrentUser());

        var entity = NewApiKey();
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        Assert.Null(entity.CreatorId);
    }

    [Fact]
    public async Task SaveChangesAsync_AddedICreationEntity_LeavesUpdatedAtUtcNull()
    {
        var fakeUser = new FakeCurrentUser(Guid.NewGuid());
        using var db = CreateContext(fakeUser);

        var entity = NewApiKey();
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        Assert.Null(entity.LastModificationTime);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedIModificationEntity_SetsUpdatedAtUtc()
    {
        var fakeUser = new FakeCurrentUser(Guid.NewGuid());
        using var db = CreateContext(fakeUser);
        var entity = NewApiKey();
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        entity.Plan = "pro";
        await db.SaveChangesAsync();

        Assert.NotNull(entity.LastModificationTime);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedIModificationEntity_SetsUpdatedByToCurrentUserId()
    {
        var userId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUser(userId);
        using var db = CreateContext(fakeUser);
        var entity = NewApiKey();
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        entity.Plan = "pro";
        await db.SaveChangesAsync();

        Assert.Equal(userId, entity.LastModifierId);
    }

    [Fact]
    public async Task SaveChangesAsync_TimestampUsesTimeProvider()
    {
        var fixedTime = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var fakeTime = new FakeTimeProvider(fixedTime);
        var fakeUser = new FakeCurrentUser(Guid.NewGuid());
        using var db = CreateContext(fakeUser, fakeTime);

        var entity = NewApiKey();
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        Assert.Equal(fixedTime, entity.CreationTime);
    }

    private static ApiKeyEntity NewApiKey() => new()
    {
        Id = Guid.NewGuid(),
        KeyHash = Guid.NewGuid().ToString(),
        UserId = Guid.NewGuid(),
        Plan = "free",
        RateLimit = 1000,
    };

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => userId;
        public string Plan => "free";
        public int RateLimit => 1000;
    }

    private sealed class NullCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid UserId => Guid.Empty;
        public string Plan => string.Empty;
        public int RateLimit => 0;
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
