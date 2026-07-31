namespace TypeGym;

public sealed record Email(string Value);

public abstract record EmailParse
{
    private EmailParse() { }

    public sealed record Accepted(Email Value) : EmailParse;

    public sealed record Rejected(string Reason) : EmailParse;
}

public static class EmailParser
{
    public static EmailParse Parse(string raw) =>
        string.IsNullOrWhiteSpace(raw) || !raw.Contains('@')
            ? new EmailParse.Rejected("invalid-email")
            : new EmailParse.Accepted(new Email(raw.Trim()));
}

public static class Challenge
{
    public static string Probe() => EmailParser.Parse("a@example.test") switch
    {
        EmailParse.Accepted accepted => accepted.Value.Value,
        EmailParse.Rejected rejected => rejected.Reason
    };
}
