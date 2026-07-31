using System.Threading.Tasks;

namespace TypeGym;

public static class AsyncBoundary
{
    public static async void FireAndForget()
    {
        await Task.Yield();
    }

    public static async Task<int> ReadAsync(Task<int> input)
    {
        await Task.Yield();
        return input.Result;
    }
}

public static class Challenge
{
    public static string Probe() => "async";
}
