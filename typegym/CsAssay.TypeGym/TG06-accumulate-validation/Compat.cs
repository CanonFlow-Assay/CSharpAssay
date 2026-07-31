using System.Collections.Immutable;

namespace TypeGym;

public abstract record Validation<T>
{
    private Validation() { }

    public sealed record Valid(T Value) : Validation<T>;

    public sealed record Invalid(ImmutableArray<string> Errors) : Validation<T>;
}

public static class Validator
{
    public static Validation<string> Person(string name, int age)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("name");
        }

        if (age < 18)
        {
            errors.Add("age");
        }

        return errors.Count == 0
            ? new Validation<string>.Valid(name)
            : new Validation<string>.Invalid(errors.ToImmutable());
    }
}

public static class Challenge
{
    public static string Probe() => Validator.Person("", 12) switch
    {
        Validation<string>.Valid valid => valid.Value,
        Validation<string>.Invalid invalid => string.Join(",", invalid.Errors)
    };
}
