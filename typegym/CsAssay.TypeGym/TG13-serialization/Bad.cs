using System;
using System.Text.Json;

namespace TypeGym;

public sealed record WireMessage(string Value);

public static class Challenge
{
    public static string Probe()
    {
        var json = JsonSerializer.Serialize(new WireMessage("created"));
        return json.Contains("created", StringComparison.Ordinal)
            ? "created"
            : "lost";
    }
}
