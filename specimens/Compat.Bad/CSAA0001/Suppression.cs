#pragma warning disable CSAA0001
using System.Threading.Tasks;

public static class AsyncVoidSample
{
    public static async void Run()
    {
        await Task.Yield();
    }
}
#pragma warning restore CSAA0001
