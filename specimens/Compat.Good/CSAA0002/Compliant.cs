using System.Threading.Tasks;

public static class AwaitedAsyncSample
{
    public static async Task<int> Run(Task<int> input) =>
        await input;
}
