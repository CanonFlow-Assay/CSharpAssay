namespace TypeGym;

public sealed record OrderRow(string OrderId);

public static class Challenge
{
    public static string Probe() => new OrderRow("O-9").OrderId;
}
