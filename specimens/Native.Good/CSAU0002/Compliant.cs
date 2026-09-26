public abstract class ClosedOutcome
{
    private ClosedOutcome() { }

    public sealed class Yes : ClosedOutcome;

    public sealed class No : ClosedOutcome;
}

public static class CompleteConsumer
{
    public static string Show(ClosedOutcome value) => value switch
    {
        ClosedOutcome.Yes yes => "yes",
        ClosedOutcome.No no => "no"
    };
}
