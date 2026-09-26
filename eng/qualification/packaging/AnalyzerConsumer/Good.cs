namespace CsAssay.Qualification;

public sealed record Accepted(string Text);
public sealed record Rejected(string Reason);
public readonly union NativeOutcome(Accepted, Rejected);

public static class Good
{
    public static string Value => string.Empty;

    public static string Describe(NativeOutcome outcome) => outcome switch
    {
        Accepted accepted => accepted.Text,
        Rejected rejected => rejected.Reason
    };
}
