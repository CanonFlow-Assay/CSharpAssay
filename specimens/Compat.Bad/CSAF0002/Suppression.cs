#pragma warning disable CSAF0002
using System.Collections.Generic;

public static class LegacyProjection
{
    public static List<int> Copy(IEnumerable<int> source)
    {
        var result = new List<int>();
        foreach (var value in source)
        {
            result.Add(value);
        }

        return result;
    }
}
#pragma warning restore CSAF0002
