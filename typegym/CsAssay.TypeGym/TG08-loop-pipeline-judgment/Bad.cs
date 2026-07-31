using System.Collections.Generic;

namespace TypeGym;

public static class Challenge
{
    public static string Probe()
    {
        var source = new[] { 1, 2, 3, 4 };
        var output = new List<int>();
        foreach (var value in source)
        {
            if (value % 2 == 0)
            {
                output.Add(value * 10);
            }
        }

        return string.Join(",", output);
    }
}
