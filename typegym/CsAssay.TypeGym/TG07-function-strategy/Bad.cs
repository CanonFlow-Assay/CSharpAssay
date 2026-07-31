namespace TypeGym;

public interface IPriceStrategy
{
    decimal Apply(decimal price);
}

public sealed class HalfPrice : IPriceStrategy
{
    public decimal Apply(decimal price) => price / 2m;
}

public static class Challenge
{
    public static string Probe() =>
        new HalfPrice().Apply(10m).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
}
