using System.Text.Json;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Redis;

public sealed class SystemTextJsonIntegrationEventSerializer : IIntegrationEventSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Serialize<TPayload>(TPayload payload)
    {
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public TPayload Deserialize<TPayload>(string payload)
    {
        var value = JsonSerializer.Deserialize<TPayload>(payload, SerializerOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Event payload deserialization returned null.");
        }

        return value;
    }
}