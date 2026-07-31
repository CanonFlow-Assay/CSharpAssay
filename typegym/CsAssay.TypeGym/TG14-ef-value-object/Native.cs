namespace TypeGym;

public readonly record struct OrderId(string Value);

public sealed class OrderIdConverter
{
    public string ToProvider(OrderId value) => value.Value;

    public OrderId FromProvider(string value) => new(value);
}

public static class Challenge
{
    public static string Probe()
    {
        var converter = new OrderIdConverter();
        return converter.ToProvider(converter.FromProvider("O-9"));
    }
}
