#pragma warning disable CSAU0001
public abstract record ClosedShape
{
    protected ClosedShape() { }
}

public sealed record OpenCase : ClosedShape;
#pragma warning restore CSAU0001
