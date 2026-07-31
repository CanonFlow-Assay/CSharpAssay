using System;

namespace TypeGym;

public readonly record struct CustomerId
{
    public CustomerId(string value)
    {
        if (!value.StartsWith("C-", StringComparison.Ordinal))
        {
            throw new ArgumentException("customer-id", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}

public readonly record struct OrderId
{
    public OrderId(string value)
    {
        if (!value.StartsWith("O-", StringComparison.Ordinal))
        {
            throw new ArgumentException("order-id", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}

public static class Challenge
{
    private static string Join(CustomerId customerId, OrderId orderId) =>
        customerId.Value + "/" + orderId.Value;

    public static string Probe() =>
        Join(new CustomerId("C-3"), new OrderId("O-7"));
}
