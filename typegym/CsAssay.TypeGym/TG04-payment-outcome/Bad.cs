namespace TypeGym;

public abstract record PaymentOutcome
{
    protected PaymentOutcome() { }
}

public sealed record Paid(string Reference) : PaymentOutcome;

public sealed record Declined(string Reason) : PaymentOutcome;

public static class Challenge
{
    public static string Show(PaymentOutcome outcome) => outcome switch
    {
        Paid paid => paid.Reference,
        _ => "other"
    };

    public static string Probe() => Show(new Paid("P-1"));
}
