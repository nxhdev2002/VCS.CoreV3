using Microsoft.AspNetCore.Http;
using VCS.CoreV3.Adapters.Web;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application.Tests;

public sealed class InternalApiServiceFilterTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenBothHeadersMissing()
    {
        var sut = new InternalApiServiceFilter(new StubValidator(false));
        var ctx = new DefaultHttpContext();

        var result = await sut.InvokeAsync(FakeContext(ctx), NotCalledNext);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCodeOf(result));
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenServiceNameHeaderMissing()
    {
        var sut = new InternalApiServiceFilter(new StubValidator(true));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[InternalServiceHeaders.ServiceKey] = "some-key";

        var result = await sut.InvokeAsync(FakeContext(ctx), NotCalledNext);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCodeOf(result));
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenServiceKeyHeaderMissing()
    {
        var sut = new InternalApiServiceFilter(new StubValidator(true));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[InternalServiceHeaders.ServiceName] = "service-a";

        var result = await sut.InvokeAsync(FakeContext(ctx), NotCalledNext);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCodeOf(result));
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenValidatorReturnsFalse()
    {
        var sut = new InternalApiServiceFilter(new StubValidator(false));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[InternalServiceHeaders.ServiceName] = "service-a";
        ctx.Request.Headers[InternalServiceHeaders.ServiceKey] = "wrong-key";

        var result = await sut.InvokeAsync(FakeContext(ctx), NotCalledNext);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCodeOf(result));
    }

    [Fact]
    public async Task InvokeAsync_CallsNext_WhenValidatorReturnsTrue()
    {
        var nextCalled = false;
        var sut = new InternalApiServiceFilter(new StubValidator(true));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[InternalServiceHeaders.ServiceName] = "service-a";
        ctx.Request.Headers[InternalServiceHeaders.ServiceKey] = "correct-key";

        await sut.InvokeAsync(FakeContext(ctx), _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PassesCorrectHeaderValuesToValidator()
    {
        string? capturedName = null;
        string? capturedKey = null;
        var sut = new InternalApiServiceFilter(new CapturingValidator((n, k) =>
        {
            capturedName = n;
            capturedKey = k;
            return true;
        }));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[InternalServiceHeaders.ServiceName] = "my-service";
        ctx.Request.Headers[InternalServiceHeaders.ServiceKey] = "my-secret";

        await sut.InvokeAsync(FakeContext(ctx), _ => ValueTask.FromResult<object?>(Results.Ok()));

        Assert.Equal("my-service", capturedName);
        Assert.Equal("my-secret", capturedKey);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCallNext_WhenValidationFails()
    {
        var nextCalled = false;
        var sut = new InternalApiServiceFilter(new StubValidator(false));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[InternalServiceHeaders.ServiceName] = "service-a";
        ctx.Request.Headers[InternalServiceHeaders.ServiceKey] = "wrong-key";

        await sut.InvokeAsync(FakeContext(ctx), _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        Assert.False(nextCalled);
    }

    private static EndpointFilterDelegate NotCalledNext =>
        _ => throw new InvalidOperationException("next should not have been called");

    private static EndpointFilterInvocationContext FakeContext(HttpContext httpContext) =>
        new FakeEndpointFilterInvocationContext(httpContext);

    private static int StatusCodeOf(object? result)
    {
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        return statusResult.StatusCode ?? 0;
    }

    private sealed class StubValidator(bool returnValue) : IInternalServiceKeyValidator
    {
        public bool Validate(string serviceName, string serviceKey) => returnValue;
    }

    private sealed class CapturingValidator(Func<string, string, bool> capture) : IInternalServiceKeyValidator
    {
        public bool Validate(string serviceName, string serviceKey) => capture(serviceName, serviceKey);
    }

    private sealed class FakeEndpointFilterInvocationContext(HttpContext httpContext) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext => httpContext;
        public override IList<object?> Arguments => [];
        public override T GetArgument<T>(int index) => throw new NotSupportedException();
    }
}
