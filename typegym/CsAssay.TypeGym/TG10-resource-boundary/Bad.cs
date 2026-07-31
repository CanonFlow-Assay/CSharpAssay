using System;
using System.IO;

namespace TypeGym;

public static class ResourceReader
{
    public static string Read(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (IOException)
        {
        }

        return "missing";
    }
}

public static class Challenge
{
    public static string Probe() =>
        ResourceReader.Read(() => throw new IOException("missing"));
}
