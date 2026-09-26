using System.Collections.Generic;

public static class Projection
{
    public static List<int> Double(IEnumerable<int> source)
    {
        var result = new List<int>();
        foreach (var value in source)
        {
            result.Add(value * 2);
        }

        return result;
    }
}
