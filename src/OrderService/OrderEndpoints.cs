using Microsoft.EntityFrameworkCore;

namespace OrderService;

public record PlaceOrderRequest(string CustomerId, IReadOnlyList<PlaceOrderItem> Items);

public record PlaceOrderItem(string Sku, int Quantity);

public record OrderResponse(
    Guid OrderId,
    string CustomerId,
    string Status,
    int TotalCents,
    string Currency,
    IReadOnlyList<OrderLineResponse> Lines);

public record OrderLineResponse(string Sku, int Quantity, int UnitPriceCents);

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", async (
            PlaceOrderRequest request,
            OrderPlacementService placement,
            CancellationToken ct) =>
        {
            if (request.Items.Count == 0)
            {
                return Results.Problem(
                    title: "Empty order",
                    detail: "An order must contain at least one item.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var command = new PlaceOrderCommand(
                request.CustomerId,
                [.. request.Items.Select(item => new OrderItemCommand(item.Sku, item.Quantity))]);

            var result = await placement.PlaceAsync(command, ct);

            return result.Outcome switch
            {
                PlaceOrderOutcome.Placed =>
                    Results.Created($"/orders/{result.Order!.Id}", ToResponse(result.Order)),

                PlaceOrderOutcome.UnknownSku => Results.Problem(
                    title: "Unknown SKU", detail: result.Detail,
                    statusCode: StatusCodes.Status400BadRequest),

                PlaceOrderOutcome.InsufficientStock => Results.Problem(
                    title: "Insufficient stock", detail: result.Detail,
                    statusCode: StatusCodes.Status409Conflict),

                PlaceOrderOutcome.PaymentDeclined => Results.Problem(
                    title: "Payment declined", detail: result.Detail,
                    statusCode: StatusCodes.Status402PaymentRequired),

                _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}.")
            };
        });

        app.MapGet("/orders/{id:guid}", async (Guid id, OrderDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders
                .AsNoTracking()
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return order is null
                ? Results.Problem(
                    title: "Unknown order", detail: $"No order exists with id '{id}'.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(ToResponse(order));
        });
    }

    private static OrderResponse ToResponse(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status,
        order.TotalCents,
        order.Currency,
        [.. order.Lines.Select(line => new OrderLineResponse(line.Sku, line.Quantity, line.UnitPriceCents))]);
}
