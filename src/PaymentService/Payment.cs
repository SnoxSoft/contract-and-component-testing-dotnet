namespace PaymentService;

public class Payment
{
    public required Guid Id { get; init; }

    public required Guid OrderId { get; init; }

    public required int AmountCents { get; init; }

    public required string Currency { get; init; }

    public required string Status { get; init; }
}

public static class PaymentStatus
{
    public const string Authorized = "Authorized";
    public const string Declined = "Declined";
}
