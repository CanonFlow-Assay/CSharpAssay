using System.Collections.Generic;
using System.Linq;

public static class Projection
{
    public static IEnumerable<int> Double(IEnumerable<int> source) =>
        source.Select(value => value * 2);
}
