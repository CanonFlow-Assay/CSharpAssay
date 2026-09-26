namespace NativeSpecimen;
public sealed record Success(string Text);
public sealed record Failure(string Message);
public union Outcome(Success, Failure);
public static class Examples
{
    public static string Show(Outcome outcome) => outcome switch
    {
        Success success => success.Text,
        _ => "hidden"
    };
}
