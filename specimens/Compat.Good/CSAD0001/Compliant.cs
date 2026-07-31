namespace Domain;

public readonly record struct CustomerId(System.Guid Value);

public static class Checkout
{
    public static void Process(CustomerId customerId) { }
}
