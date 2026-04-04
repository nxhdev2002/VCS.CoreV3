using Microsoft.AspNetCore.Http;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure;

public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly ApiKeyEntity? _entity;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        var items = httpContextAccessor.HttpContext?.Items;
        _entity = items?[ApiKeyHttpContextKeys.ResolvedEntity] as ApiKeyEntity;
    }

    public bool IsAuthenticated => _entity is not null;
    public Guid UserId => _entity?.UserId ?? Guid.Empty;
    public string Plan => _entity?.Plan ?? string.Empty;
    public int RateLimit => _entity?.RateLimit ?? 0;
}
