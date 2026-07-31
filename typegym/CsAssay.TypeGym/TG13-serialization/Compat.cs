using System.Text.Json;

namespace TypeGym;

public abstract record DomainEvent
{
    private DomainEvent() { }

    public sealed record Created(string Name) : DomainEvent;

    public sealed record Removed(string Name) : DomainEvent;
}

public static class EventWire
{
    public static string Write(DomainEvent value) => value switch
    {
        DomainEvent.Created created => JsonSerializer.Serialize(
            new WireCase("created", created.Name)),
        DomainEvent.Removed removed => JsonSerializer.Serialize(
            new WireCase("removed", removed.Name))
    };

    public static DomainEvent Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var kind = root.GetProperty("Kind").GetString();
        var name = root.GetProperty("Name").GetString();
        if (kind is not string requiredKind || name is not string requiredName)
        {
            throw new JsonException("Missing event fields.");
        }

        return requiredKind switch
        {
            "created" => new DomainEvent.Created(requiredName),
            "removed" => new DomainEvent.Removed(requiredName),
            _ => throw new JsonException("Unknown event kind.")
        };
    }

    private sealed record WireCase(string Kind, string Name);
}

public static class Challenge
{
    public static string Probe()
    {
        var json = EventWire.Write(new DomainEvent.Created("order"));
        return EventWire.Read(json) switch
        {
            DomainEvent.Created created => "created:" + created.Name,
            DomainEvent.Removed removed => "removed:" + removed.Name
        };
    }
}
