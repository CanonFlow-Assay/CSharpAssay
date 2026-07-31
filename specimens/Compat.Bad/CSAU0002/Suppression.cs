#pragma warning disable CSAU0002
public abstract class ClosedOutcome
{
    private ClosedOutcome() { }

    public sealed class Yes : ClosedOutcome;

    public sealed class No : ClosedOutcome;
}

public static class IncompleteConsumer
{
    public static string Show(ClosedOutcome value) => value switch
    {
        ClosedOutcome.Yes => "yes",
        _ => "other"
    };
}
#pragma warning restore CSAU0002
