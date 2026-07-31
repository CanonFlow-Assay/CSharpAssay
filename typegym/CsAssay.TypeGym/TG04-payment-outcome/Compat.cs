namespace TypeGym;

public abstract class PaymentOutcome
{
    private PaymentOutcome() { }

    public sealed class Paid(string reference) : PaymentOutcome
    {
        public string Reference { get; } = reference;
    }

    public sealed class Declined(string reason) : PaymentOutcome
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
