namespace TypeGym;

public static class Challenge
{
    private static string Join(string customerId, string orderId) =>
        customerId + "/" + orderId;

    public static string Probe() => Join("C-3", "O-7");
}
