namespace VCS.CoreV3.Adapters.Web;

public static class InternalApiServiceEndpointExtensions
{
    public static RouteHandlerBuilder WithInternalApiService(this RouteHandlerBuilder builder)
    {
        return builder
            .AddEndpointFilter<InternalApiServiceFilter>()
            .WithMetadata(new InternalApiServiceAttribute())
            .ExcludeFromDescription();
    }
}
