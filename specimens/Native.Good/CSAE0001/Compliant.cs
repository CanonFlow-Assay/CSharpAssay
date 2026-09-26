using System;

public static class ObservedFailure
{
    public static void Run()
    {
        try { throw new InvalidOperationException(); }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
        }
    }
}
