#pragma warning disable CSAN0002
public static class NullForgivingSample
{
    public static string Read(string value) => value!;
}
#pragma warning restore CSAN0002
