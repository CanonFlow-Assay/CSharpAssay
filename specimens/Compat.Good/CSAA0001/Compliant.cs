using System.Threading.Tasks;

public static class AsyncTaskSample
{
    public static async Task Run()
    {
        await Task.Yield();
    }
}
