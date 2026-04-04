using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Infrastructure.Data;
using VCS.CoreV3.Domain.Entities;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests.Data
{
    public class PostgresApiKeyRepositoryTests
    {
        private AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
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
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false,
                ExpiredAt = null,
                Plan = "free",
                RateLimit = 1000,
                UpdatedAt = DateTime.UtcNow
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
                CreatedAt = DateTime.UtcNow,
                IsRevoked = true,
                ExpiredAt = null,
                Plan = "free",
                RateLimit = 1000,
                UpdatedAt = DateTime.UtcNow
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
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                IsRevoked = false,
                ExpiredAt = DateTime.UtcNow.AddDays(-1),
                Plan = "free",
                RateLimit = 1000,
                UpdatedAt = DateTime.UtcNow
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
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false,
                ExpiredAt = null,
                Plan = "free",
                RateLimit = 1000,
                UpdatedAt = DateTime.UtcNow
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();

            var result = await repo.RevokeAsync(key.Id);
            Assert.True(result);
            var updated = await db.ApiKeys.FindAsync(key.Id);
            Assert.True(updated!.IsRevoked);
        }
    }
}