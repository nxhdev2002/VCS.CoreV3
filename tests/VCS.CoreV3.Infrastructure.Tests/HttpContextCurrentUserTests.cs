using System;
using Microsoft.AspNetCore.Http;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests;

public sealed class HttpContextCurrentUserTests
{
    [Fact]
    public void IsAuthenticated_False_WhenNoApiKeyItemInContext()
    {
        var sut = new HttpContextCurrentUser(new FakeHttpContextAccessor(new DefaultHttpContext()));

        Assert.False(sut.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_True_WhenEntitySetInContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Items[ApiKeyHttpContextKeys.ResolvedEntity] = ValidEntity();
        var sut = new HttpContextCurrentUser(new FakeHttpContextAccessor(ctx));

        Assert.True(sut.IsAuthenticated);
    }

    [Fact]
    public void Properties_ReturnDefaults_WhenNotAuthenticated()
    {
        var sut = new HttpContextCurrentUser(new FakeHttpContextAccessor(new DefaultHttpContext()));

        Assert.Equal(Guid.Empty, sut.UserId);
        Assert.Equal(string.Empty, sut.Plan);
        Assert.Equal(0, sut.RateLimit);
    }

    [Fact]
    public void Properties_ReturnEntityValues_WhenAuthenticated()
    {
        var entity = ValidEntity();
        var ctx = new DefaultHttpContext();
        ctx.Items[ApiKeyHttpContextKeys.ResolvedEntity] = entity;
        var sut = new HttpContextCurrentUser(new FakeHttpContextAccessor(ctx));

        Assert.Equal(entity.UserId, sut.UserId);
        Assert.Equal("pro", sut.Plan);
        Assert.Equal(5000, sut.RateLimit);
    }

    [Fact]
    public void IsAuthenticated_False_WhenHttpContextIsNull()
    {
        var sut = new HttpContextCurrentUser(new FakeHttpContextAccessor(null));

        Assert.False(sut.IsAuthenticated);
    }

    private static ApiKeyEntity ValidEntity() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        KeyHash = "abc",
        Plan = "pro",
        RateLimit = 5000,
    };

    private sealed class FakeHttpContextAccessor(HttpContext? context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get => context; set { } }
    }
}
