using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Infrastructure;
using VCS.CoreV3.Infrastructure.Data;
using VCS.CoreV3.Domain.Entities;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests.Data
{
    public class PostgresApiKeyRepositoryTests
    {
        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options, new NullCurrentUser(), TimeProvider.System);
        }

        [Fact]
        public async Task GetByKeyHashAsync_Returns_ActiveKey()
        {
            var db = CreateDbContext();
            var repo = new PostgresApiKeyRepository(db);
            var key = new ApiKeyEntity
            {
                Id = Guid.NewGuid(),
                KeyHash = "hash1",
                UserId = Guid.NewGuid(),
                IsRevoked = false,
                ExpiredAt = null,
                Plan = "free",
                RateLimit = 1000,
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();

            var result = await repo.GetByKeyHashAsync("hash1");
            Assert.NotNull(result);
            Assert.Equal("hash1", result!.KeyHash);
        }

        [Fact]
        public async Task GetByKeyHashAsync_Returns_Null_If_Revoked()
        {
            var db = CreateDbContext();
            var repo = new PostgresApiKeyRepository(db);
            var key = new ApiKeyEntity
            {
                Id = Guid.NewGuid(),
                KeyHash = "hash2",
                UserId = Guid.NewGuid(),
                IsRevoked = true,
                ExpiredAt = null,
                Plan = "free",
                RateLimit = 1000,
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();

            var result = await repo.GetByKeyHashAsync("hash2");
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByKeyHashAsync_Returns_Null_If_Expired()
        {
            var db = CreateDbContext();
            var repo = new PostgresApiKeyRepository(db);
            var key = new ApiKeyEntity
            {
                Id = Guid.NewGuid(),
                KeyHash = "hash3",
                UserId = Guid.NewGuid(),
                IsRevoked = false,
                ExpiredAt = DateTime.UtcNow.AddDays(-1),
                Plan = "free",
                RateLimit = 1000,
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();

            var result = await repo.GetByKeyHashAsync("hash3");
            Assert.Null(result);
        }

        [Fact]
        public async Task RevokeAsync_Sets_IsRevoked_True()
        {
            var db = CreateDbContext();
            var repo = new PostgresApiKeyRepository(db);
            var key = new ApiKeyEntity
            {
                Id = Guid.NewGuid(),
                KeyHash = "hash4",
                UserId = Guid.NewGuid(),
                IsRevoked = false,
                ExpiredAt = null,
                Plan = "free",
                RateLimit = 1000,
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();

            var result = await repo.RevokeAsync(key.Id);
            Assert.True(result);
            var updated = await db.ApiKeys.FindAsync(key.Id);
            Assert.True(updated!.IsRevoked);
        }

        [Fact]
        public async Task UpdateRateLimitAsync_Updates_RateLimit_And_Returns_True()
        {
            var db = CreateDbContext();
            var repo = new PostgresApiKeyRepository(db);
            var key = new ApiKeyEntity
            {
                Id = Guid.NewGuid(),
                KeyHash = "hash5",
                UserId = Guid.NewGuid(),
                IsRevoked = false,
                ExpiredAt = null,
                Plan = "free",
                RateLimit = 1000,
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();

            var result = await repo.UpdateRateLimitAsync(key.Id, 9999);

            Assert.True(result);
            var updated = await db.ApiKeys.FindAsync(key.Id);
            Assert.Equal(9999, updated!.RateLimit);
        }

        [Fact]
        public async Task UpdateRateLimitAsync_Returns_False_WhenKeyNotFound()
        {
            var db = CreateDbContext();
            var repo = new PostgresApiKeyRepository(db);

            var result = await repo.UpdateRateLimitAsync(Guid.NewGuid(), 500);

            Assert.False(result);
        }
    }
}