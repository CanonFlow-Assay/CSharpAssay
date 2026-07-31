using System.Threading;
using System.Threading.Tasks;

namespace TypeGym;

public static class AsyncBoundary
{
    public static async Task<int> ReadAsync(
        Task<int> input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await input.WaitAsync(cancellationToken);
    }
}

public static class Challenge
{
    public static string Probe() => "async";
}
