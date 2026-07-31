using System;
using System.IO;

namespace TypeGym;

public abstract record ReadResult
{
    private ReadResult() { }

    public sealed record Content(string Value) : ReadResult;

    public sealed record Failure(string Reason) : ReadResult;
}

public static class ResourceReader
{
    public static ReadResult Read(Func<string> read)
    {
        try
        {
            return new ReadResult.Content(read());
        }
        catch (IOException exception)
        {
            return new ReadResult.Failure(exception.Message);
        }
    }
}

public static class Challenge
{
    public static string Probe() =>
        ResourceReader.Read(() => throw new IOException("missing")) switch
        {
            ReadResult.Content content => content.Value,
            ReadResult.Failure failure => failure.Reason
        };
}
