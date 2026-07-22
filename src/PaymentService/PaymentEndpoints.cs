namespace PaymentService;

public record AuthorisePaymentRequest(Guid OrderId, int AmountCents, string Currency);

public record PaymentResponse(Guid PaymentId, Guid OrderId, int AmountCents, string Currency, string Status);

public static class PaymentEndpoints
{
    // Deterministic rule so tests can reliably trigger a decline.
    private const int CreditLimitCents = 50_000;

    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/payments", async (
            AuthorisePaymentRequest request,
            PaymentDbContext db,
            CancellationToken ct) =>
        {
            if (request.AmountCents <= 0)
            {
                return Results.Problem(
                    title: "Invalid amount",
                    detail: "AmountCents must be greater than zero.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var declined = request.AmountCents > CreditLimitCents;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                AmountCents = request.AmountCents,
                Currency = request.Currency,
                Status = declined ? PaymentStatus.Declined : PaymentStatus.Authorized
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            var response = new PaymentResponse(
                payment.Id, payment.OrderId, payment.AmountCents, payment.Currency, payment.Status);

            return declined
                ? Results.Json(response, statusCode: StatusCodes.Status422UnprocessableEntity)
                : Results.Created($"/payments/{payment.Id}", response);
        });

        app.MapGet("/payments/{id:guid}", async (Guid id, PaymentDbContext db, CancellationToken ct) =>
        {
            var payment = await db.Payments.FindAsync([id], ct);

            return payment is null
                ? Results.Problem(
                    title: "Unknown payment",
                    detail: $"No payment exists with id '{id}'.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(new PaymentResponse(
                    payment.Id, payment.OrderId, payment.AmountCents, payment.Currency, payment.Status));
        });
    }
}
