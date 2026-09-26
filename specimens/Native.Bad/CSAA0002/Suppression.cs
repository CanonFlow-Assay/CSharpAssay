#pragma warning disable CSAA0002
using System.Threading.Tasks;

public static class BlockingAsyncSample
{
    public static async Task<int> Run(Task<int> input)
    {
        await Task.Yield();
        return input.Result;
    }
}
#pragma warning restore CSAA0002
