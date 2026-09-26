public abstract record ClosedShape
{
    protected ClosedShape() { }
}

public sealed record OpenCase : ClosedShape;
