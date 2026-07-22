namespace Contracts;

public record OrderPlaced
{
    public required Guid OrderId { get; init; }

    public required string CustomerId { get; init; }

    public required int TotalCents { get; init; }

    public required string Currency { get; init; }

    public required DateTimeOffset PlacedAt { get; init; }

    public required IReadOnlyList<OrderPlacedLine> Lines { get; init; }
}

public record OrderPlacedLine
{
    public required string Sku { get; init; }

    public required int Quantity { get; init; }

    public required int UnitPriceCents { get; init; }
}
