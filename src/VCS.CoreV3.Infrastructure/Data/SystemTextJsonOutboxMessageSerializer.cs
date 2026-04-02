using System.Text.Json;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class SystemTextJsonOutboxMessageSerializer : IOutboxMessageSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Serialize<TPayload>(IntegrationEvent<TPayload> integrationEvent)
    {
        return JsonSerializer.Serialize(integrationEvent, SerializerOptions);
    }

    public IntegrationEvent<TPayload> Deserialize<TPayload>(string payload)
    {
        var value = JsonSerializer.Deserialize<IntegrationEvent<TPayload>>(payload, SerializerOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Outbox message deserialization returned null.");
        }

        return value;
    }
}