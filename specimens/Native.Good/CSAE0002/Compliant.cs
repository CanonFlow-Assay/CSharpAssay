public abstract record EmailParse
{
    private EmailParse() { }

    public sealed record Valid(string Value) : EmailParse;
    public sealed record Invalid(string Reason) : EmailParse;
}
