using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Adapters.Web;

internal static class ApiKeyEndpoint
{
    public static void MapApiKeyEndpoint(this WebApplication app)
    {
        app.MapPost("/internal/api-keys", async (
            CreateApiKeyRequest request,
            ICreateApiKeyUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(request.UserId, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithInternalApiService()
        .WithName("CreateApiKey")
        .WithTags("ApiKeys")
        .WithSummary("Create a new API key")
        .WithDescription("Creates a new API key for a user. Requires internal service authentication via X-Service-Name and X-Service-Key headers.");
    }
}

internal sealed record CreateApiKeyRequest(Guid UserId);
