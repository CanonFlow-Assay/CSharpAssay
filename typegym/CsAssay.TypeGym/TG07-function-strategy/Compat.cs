using System;

namespace TypeGym;

public static class Pricing
{
    public static decimal Apply(decimal price, Func<decimal, decimal> strategy) =>
        strategy(price);
}

public static class Challenge
{
    public static string Probe() =>
        Pricing.Apply(10m, price => price / 2m).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
}
