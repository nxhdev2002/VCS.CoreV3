using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Adapters.Web;

public sealed class InternalApiServiceFilter : IEndpointFilter
{
    private readonly IInternalServiceKeyValidator _validator;

    public InternalApiServiceFilter(IInternalServiceKeyValidator validator)
    {
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var headers = context.HttpContext.Request.Headers;

        var serviceName = headers[InternalServiceHeaders.ServiceName].ToString();
        var serviceKey = headers[InternalServiceHeaders.ServiceKey].ToString();

        if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(serviceKey))
        {
            return Results.Unauthorized();
        }

        if (!_validator.Validate(serviceName, serviceKey))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
