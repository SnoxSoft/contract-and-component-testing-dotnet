namespace OrderService;

public class Order
{
    public required Guid Id { get; init; }

    public required string CustomerId { get; init; }

    public required string Status { get; set; }

    public required int TotalCents { get; set; }

    public required string Currency { get; init; }

    public required DateTimeOffset PlacedAt { get; init; }

    public List<OrderLine> Lines { get; init; } = [];
}

public class OrderLine
{
    public int Id { get; init; }

    public Guid OrderId { get; init; }

    public required string Sku { get; init; }

    public required int Quantity { get; init; }

    public required int UnitPriceCents { get; init; }
}

public static class OrderStatus
{
    public const string Placed = "Placed";
    public const string PaymentDeclined = "PaymentDeclined";
}
