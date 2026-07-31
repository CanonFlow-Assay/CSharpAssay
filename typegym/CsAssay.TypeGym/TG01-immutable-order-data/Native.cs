using System.Collections.Immutable;

namespace TypeGym;

public sealed record Order(string Id, ImmutableArray<string> Lines);

public static class Challenge
{
    public static string Probe()
    {
        var order = new Order("A", ["x", "y"]);
        return order.Id + ":" + order.Lines.Length;
    }
}
