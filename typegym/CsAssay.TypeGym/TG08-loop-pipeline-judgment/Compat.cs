using System.Linq;

namespace TypeGym;

public static class Challenge
{
    public static string Probe()
    {
        var output = new[] { 1, 2, 3, 4 }
            .Where(value => value % 2 == 0)
            .Select(value => value * 10);
        return string.Join(",", output);
    }
}
