namespace Qualification.Domain;

public readonly record struct CustomerId(System.Guid Value);

public static class Checkout
{
    public static void Raw(System.Guid customerId) { }

    public static void Typed(CustomerId customerId) { }
}
