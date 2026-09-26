public static class PriceFunctions
{
    public static decimal Calculate(
        decimal value,
        System.Func<decimal, decimal> strategy) =>
        strategy(value);
}
