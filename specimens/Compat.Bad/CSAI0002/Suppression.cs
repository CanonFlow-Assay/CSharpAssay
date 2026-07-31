#pragma warning disable CSAI0002
using System.Collections.Generic;

public sealed record MutableLines
{
    public List<string> Lines { get; init; } = [];
}
#pragma warning restore CSAI0002
