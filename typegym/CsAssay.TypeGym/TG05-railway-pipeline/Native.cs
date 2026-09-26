using System;

namespace TypeGym;

public abstract record Result<T>
{
    private Result() { }

    public sealed record Ok(T Value) : Result<T>;

    public sealed record Error(string Reason) : Result<T>;
}

public static class Railway
{
    public static Result<R> Map<T, R>(Result<T> input, Func<T, R> map) =>
        input switch
        {
            Result<T>.Ok ok => new Result<R>.Ok(map(ok.Value)),
            Result<T>.Error error => new Result<R>.Error(error.Reason)
        };

    public static Result<R> Bind<T, R>(
        Result<T> input,
        Func<T, Result<R>> bind) =>
        input switch
        {
            Result<T>.Ok ok => bind(ok.Value),
            Result<T>.Error error => new Result<R>.Error(error.Reason)
        };
}

public static class Challenge
{
    public static string Probe()
    {
        Result<int> start = new Result<int>.Ok(20);
        var doubled = Railway.Map(start, value => value * 2);
        return doubled switch
        {
            Result<int>.Ok ok => ok.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            Result<int>.Error error => error.Reason
        };
    }
}
