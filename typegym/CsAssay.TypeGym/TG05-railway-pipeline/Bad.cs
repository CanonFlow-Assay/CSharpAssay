namespace TypeGym;

public static class Challenge
{
    public static string Probe()
    {
        var parsed = int.Parse("20", System.Globalization.CultureInfo.InvariantCulture);
        var doubled = parsed * 2;
        return doubled.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
