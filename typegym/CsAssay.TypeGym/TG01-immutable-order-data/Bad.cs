using System.Collections.Generic;

namespace TypeGym;

public sealed record Order
{
    public string Id { get; set; } = "";
    public List<string> Lines { get; init; } = [];
}

public static class Challenge
{
    public static string Probe()
    {
        var order = new Order { Id = "A", Lines = ["x", "y"] };
        return order.Id + ":" + order.Lines.Count;
    }
}
