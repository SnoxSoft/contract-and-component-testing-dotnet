using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace OrderService.ComponentTests;

/// <summary>
/// Canned collaborator responses. Every field name here is an assumption about
/// what the real provider returns, and nothing in this project verifies it.
/// </summary>
internal static class StubExtensions
{
    public static void StubStock(
        this OrderApiFactory factory, string sku, int availableQuantity, int unitPriceCents) =>
        factory.Inventory
            .Given(Request.Create().WithPath($"/stock/{sku}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { sku, availableQuantity, unitPriceCents }));

    public static void StubStockNotFound(this OrderApiFactory factory, string sku) =>
        factory.Inventory
            .Given(Request.Create().WithPath($"/stock/{sku}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

    public static void StubPaymentAuthorised(this OrderApiFactory factory) =>
        factory.Payments
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    paymentId = Guid.NewGuid(),
                    orderId = Guid.NewGuid(),
                    amountCents = 1_798,
                    currency = "EUR",
                    status = "Authorized"
                }));
}
