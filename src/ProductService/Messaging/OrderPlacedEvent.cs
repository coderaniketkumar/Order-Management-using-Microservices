namespace ProductService.Messaging;

public record OrderPlacedEvent(int OrderId, int ProductId, int Quantity, DateTime OccurredAtUtc);

public static class Queues
{
    public const string OrderPlaced = "order-placed";
}