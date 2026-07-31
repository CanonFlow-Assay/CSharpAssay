namespace TypeGym;

public static class Challenge
{
    private static string Validate(string name, int age)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "name";
        }

        if (age < 18)
        {
            return "age";
        }

        return "valid";
    }

    public static string Probe() => Validate("", 12);
}
