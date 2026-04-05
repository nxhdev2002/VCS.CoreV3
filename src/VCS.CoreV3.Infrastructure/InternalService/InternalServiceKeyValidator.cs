using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.InternalService;

public sealed class InternalServiceKeyValidator : IInternalServiceKeyValidator
{
    private readonly InternalApiOptions _options;

    public InternalServiceKeyValidator(IOptions<InternalApiOptions> options)
    {
        _options = options.Value;
    }

    public bool Validate(string serviceName, string serviceKey)
    {
        if (!_options.AllowedServices.TryGetValue(serviceName, out var expectedKey))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var actualBytes = Encoding.UTF8.GetBytes(serviceKey);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
