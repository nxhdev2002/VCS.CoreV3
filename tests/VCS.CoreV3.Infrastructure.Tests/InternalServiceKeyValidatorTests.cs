using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VCS.CoreV3.Infrastructure.InternalService;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests;

public sealed class InternalServiceKeyValidatorTests
{
    [Fact]
    public void Validate_ReturnsTrue_WhenServiceNameAndKeyMatch()
    {
        var sut = BuildSut(new Dictionary<string, string>
        {
            ["service-a"] = "correct-key"
        });

        Assert.True(sut.Validate("service-a", "correct-key"));
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenServiceNameNotRegistered()
    {
        var sut = BuildSut(new Dictionary<string, string>
        {
            ["service-a"] = "correct-key"
        });

        Assert.False(sut.Validate("unknown-service", "correct-key"));
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenKeyDoesNotMatch()
    {
        var sut = BuildSut(new Dictionary<string, string>
        {
            ["service-a"] = "correct-key"
        });

        Assert.False(sut.Validate("service-a", "wrong-key"));
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenAllowedServicesIsEmpty()
    {
        var sut = BuildSut([]);

        Assert.False(sut.Validate("service-a", "any-key"));
    }

    [Fact]
    public void Validate_ReturnsFalse_WhenKeyIsEmpty()
    {
        var sut = BuildSut(new Dictionary<string, string>
        {
            ["service-a"] = "correct-key"
        });

        Assert.False(sut.Validate("service-a", string.Empty));
    }

    [Fact]
    public void Validate_IsCaseSensitive_ForServiceName()
    {
        var sut = BuildSut(new Dictionary<string, string>
        {
            ["service-a"] = "correct-key"
        });

        Assert.False(sut.Validate("Service-A", "correct-key"));
    }

    [Fact]
    public void Validate_IsCaseSensitive_ForKey()
    {
        var sut = BuildSut(new Dictionary<string, string>
        {
            ["service-a"] = "SecretKey"
        });

        Assert.False(sut.Validate("service-a", "secretkey"));
    }

    [Fact]
    public void Validate_SupportsMultipleRegisteredServices()
    {
        var sut = BuildSut(new Dictionary<string, string>
        {
            ["service-a"] = "key-a",
            ["service-b"] = "key-b"
        });

        Assert.True(sut.Validate("service-a", "key-a"));
        Assert.True(sut.Validate("service-b", "key-b"));
        Assert.False(sut.Validate("service-a", "key-b"));
    }

    private static InternalServiceKeyValidator BuildSut(Dictionary<string, string> services)
    {
        var options = Options.Create(new InternalApiOptions { AllowedServices = services });
        return new InternalServiceKeyValidator(options);
    }
}
