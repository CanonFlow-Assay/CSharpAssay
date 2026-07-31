public static class Email
{
    public static string Parse(string value) =>
        value.Length > 0
            ? value
            : throw new System.ArgumentException("empty", nameof(value));
}
