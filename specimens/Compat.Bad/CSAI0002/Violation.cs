using System.Collections.Generic;

public sealed record MutableLines
{
    public List<string> Lines { get; init; } = [];
}
