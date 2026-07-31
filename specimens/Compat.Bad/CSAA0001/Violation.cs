using System.Threading.Tasks;

public static class AsyncVoidSample
{
    public static async void Run()
    {
        await Task.Yield();
    }
}
