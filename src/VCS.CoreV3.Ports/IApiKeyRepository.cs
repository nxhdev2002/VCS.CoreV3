using System;
using System.Threading.Tasks;
using VCS.CoreV3.Domain.Entities;

namespace VCS.CoreV3.Ports
{
    public interface IApiKeyRepository
    {
        Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash);
        Task<bool> RevokeAsync(Guid id);
        Task<bool> UpdateRateLimitAsync(Guid id, int newRateLimit);
        Task CreateAsync(ApiKeyEntity entity, CancellationToken ct = default);
    }
}