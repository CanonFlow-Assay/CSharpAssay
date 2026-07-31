namespace TypeGym;

public sealed record Email(string Value)
{
    public static Email? TryCreate(string? raw) =>
        string.IsNullOrWhiteSpace(raw) || !raw.Contains('@')
            ? null
            : new Email(raw);
}

public static class Challenge
{
    public static string Probe() =>
        Email.TryCreate("a@example.test")?.Value ?? "invalid";
}
