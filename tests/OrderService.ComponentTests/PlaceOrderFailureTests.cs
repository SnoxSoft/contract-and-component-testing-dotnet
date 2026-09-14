using System.Net;
using System.Net.Http.Json;
using Contracts;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace OrderService.ComponentTests;

[Collection(nameof(OrderCollection))]
public class PlaceOrderFailureTests(OrderApiFactory factory) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Reports_payment_required_when_the_payment_is_declined()
    {
        var ct = TestContext.Current.CancellationToken;
        var customerId = $"cust-{Guid.NewGuid():N}";

        factory.StubStock("SKU-COFFEE", availableQuantity: 500, unitPriceCents: 899);
        factory.Payments
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(422)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    paymentId = Guid.NewGuid(),
                    orderId = Guid.NewGuid(),
                    amountCents = 89_900,
                    currency = "EUR",
                    status = "Declined"
                }));

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/orders", new PlaceOrderRequest(customerId, [new PlaceOrderItem("SKU-COFFEE", 100)]), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);

        // The harness keeps every message published since the host started, and a reset
        // does not clear it, so the assertion is scoped to this test's own customer.
        var harness = factory.Services.GetRequiredService<ITestHarness>();
        harness.Published.Select<OrderPlaced>(ct)
            .Any(m => m.Context.Message.CustomerId == customerId)
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Rejects_an_empty_order_without_calling_anyone()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/orders", new PlaceOrderRequest("cust-1", []), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        factory.Inventory.LogEntries.ShouldBeEmpty();
        factory.Payments.LogEntries.ShouldBeEmpty();
    }
}

