namespace TypeGym;

public abstract record LookupResult
{
    private LookupResult() { }

    public sealed record Found(string Value) : LookupResult;

    public sealed record Missing : LookupResult;
}

public sealed record HttpResponse(int Status, string Body);

public static class HttpAdapter
{
    public static HttpResponse Adapt(LookupResult result) => result switch
    {
        LookupResult.Found found => new HttpResponse(200, found.Value),
        LookupResult.Missing => new HttpResponse(404, "not-found")
    };
}

public static class Challenge
{
    public static string Probe() =>
        HttpAdapter.Adapt(new LookupResult.Found("ok")).Status
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
}
