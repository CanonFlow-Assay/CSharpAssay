#pragma warning disable CSAE0001
using System;

public static class SwallowedFailure
{
    public static void Run()
    {
        try { throw new InvalidOperationException(); }
        catch { }
    }
}
#pragma warning restore CSAE0001
