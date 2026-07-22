using Contracts;
using MassTransit;
using OrderService.Clients;

namespace OrderService;

public record PlaceOrderCommand(string CustomerId, IReadOnlyList<OrderItemCommand> Items);

public record OrderItemCommand(string Sku, int Quantity);

public enum PlaceOrderOutcome
{
    Placed,
    UnknownSku,
    InsufficientStock,
    PaymentDeclined
}

public record PlaceOrderResult(PlaceOrderOutcome Outcome, Order? Order, string? Detail);

public class OrderPlacementService(
    OrderDbContext db,
    InventoryClient inventory,
    PaymentClient payments,
    IPublishEndpoint publishEndpoint,
    TimeProvider timeProvider)
{
    private const string Currency = "EUR";

    public async Task<PlaceOrderResult> PlaceAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        var lines = new List<OrderLine>();

        foreach (var item in command.Items)
        {
            var stock = await inventory.GetStockAsync(item.Sku, ct);

            if (stock is null)
            {
                return new PlaceOrderResult(
                    PlaceOrderOutcome.UnknownSku, null, $"SKU '{item.Sku}' does not exist.");
            }

            if (stock.AvailableQuantity < item.Quantity)
            {
                return new PlaceOrderResult(
                    PlaceOrderOutcome.InsufficientStock, null,
                    $"SKU '{item.Sku}' has {stock.AvailableQuantity} available but {item.Quantity} were requested.");
            }

            lines.Add(new OrderLine
            {
                Sku = stock.Sku,
                Quantity = item.Quantity,
                UnitPriceCents = stock.UnitPriceCents
            });
        }

        var orderId = Guid.NewGuid();
        var totalCents = lines.Sum(line => line.Quantity * line.UnitPriceCents);

        var payment = await payments.AuthoriseAsync(
            new AuthorisePaymentRequest(orderId, totalCents, Currency), ct);

        var declined = payment.Status != PaymentClient.Authorized;

        var order = new Order
        {
            Id = orderId,
            CustomerId = command.CustomerId,
            Currency = Currency,
            TotalCents = totalCents,
            PlacedAt = timeProvider.GetUtcNow(),
            Status = declined ? OrderStatus.PaymentDeclined : OrderStatus.Placed,
            Lines = lines
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        if (declined)
        {
            return new PlaceOrderResult(
                PlaceOrderOutcome.PaymentDeclined, order, "Payment was declined.");
        }

        await publishEndpoint.Publish(
            new OrderPlaced
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                TotalCents = order.TotalCents,
                Currency = order.Currency,
                PlacedAt = order.PlacedAt,
                Lines = [.. order.Lines.Select(line =>
                    new OrderPlacedLine
                    {
                        Sku = line.Sku,
                        Quantity = line.Quantity,
                        UnitPriceCents = line.UnitPriceCents
                    })]
            },
            ct);

        return new PlaceOrderResult(PlaceOrderOutcome.Placed, order, null);
    }
}
