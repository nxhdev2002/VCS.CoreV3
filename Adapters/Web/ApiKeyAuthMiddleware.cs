using System.Security.Cryptography;
using System.Text;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Adapters.Web;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context, IApiKeyRepository apiKeyRepository)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var rawKey)
            && !string.IsNullOrWhiteSpace(rawKey))
        {
            var hash = ComputeSha256Hash(rawKey!);
            var entity = await apiKeyRepository.GetByKeyHashAsync(hash);
            if (entity is not null)
            {
                context.Items[ApiKeyHttpContextKeys.ResolvedEntity] = entity;
            }
        }

        await next(context);
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
