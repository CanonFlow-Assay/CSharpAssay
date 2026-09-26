namespace TypeGym;

public sealed record Accepted(string Text);
public sealed record Rejected(string Reason);
public union Outcome(Accepted, Rejected);

public static class Challenge
{
    public static string Read(Outcome value) => value switch
    {
        Accepted accepted => accepted.Text,
        Rejected rejected => rejected.Reason
    };

    public static string Probe() => Read(new Accepted("native"));
}
