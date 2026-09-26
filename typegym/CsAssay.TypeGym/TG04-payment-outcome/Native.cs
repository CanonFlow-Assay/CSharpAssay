namespace TypeGym;

public union PaymentOutcome(PaymentOutcome.Paid, PaymentOutcome.Declined)
{
    public sealed class Paid(string reference)
    {
        public string Reference { get; } = reference;
    }

    public sealed class Declined(string reason)
    {
        public string Reason { get; } = reason;
    }
}

public static class Challenge
{
    public static string Show(PaymentOutcome outcome) => outcome switch
    {
        PaymentOutcome.Paid paid => paid.Reference,
        PaymentOutcome.Declined declined => declined.Reason
    };

    public static string Probe() => Show(new PaymentOutcome.Paid("P-1"));
}
