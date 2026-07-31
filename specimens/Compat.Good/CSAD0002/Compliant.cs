public abstract record DeliveryState
{
    private DeliveryState() { }

    public sealed record Pending : DeliveryState;
    public sealed record Dispatched : DeliveryState;
}
