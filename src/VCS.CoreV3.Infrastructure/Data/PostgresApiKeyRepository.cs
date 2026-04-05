using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Data
{
    public class PostgresApiKeyRepository : IApiKeyRepository
    {
        private readonly AppDbContext _db;
        public PostgresApiKeyRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash)
        {
            return await _db.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.KeyHash == keyHash && !x.IsRevoked && (x.ExpiredAt == null || x.ExpiredAt > DateTime.UtcNow));
        }

        public async Task<bool> RevokeAsync(Guid id)
        {
            var entity = await _db.ApiKeys.FindAsync(id);
            if (entity == null) return false;
            entity.IsRevoked = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateRateLimitAsync(Guid id, int newRateLimit)
        {
            var entity = await _db.ApiKeys.FindAsync(id);
            if (entity == null) return false;
            entity.RateLimit = newRateLimit;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task CreateAsync(ApiKeyEntity entity, CancellationToken ct = default)
        {
            _db.ApiKeys.Add(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}