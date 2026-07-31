namespace CsAssay.Runner;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        return CommandApp.RunAsync(
            args,
            Console.Out,
            Console.Error,
            cancellation.Token);
    }
}
