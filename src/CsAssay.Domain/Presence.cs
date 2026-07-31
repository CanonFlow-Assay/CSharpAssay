namespace CsAssay.Domain;

public abstract record Presence<T>
    where T : notnull
{
    private protected Presence()
    {
    }

    public sealed record Absent : Presence<T>;

    public sealed record Present(T Value) : Presence<T>;
}

public static class Presence
{
    public static Presence<T> Missing<T>()
        where T : notnull =>
        new Presence<T>.Absent();

    public static Presence<T> Of<T>(T value)
        where T : notnull
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new Presence<T>.Present(value);
    }
}
