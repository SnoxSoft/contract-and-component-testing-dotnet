using System.Net;
using System.Net.Http.Json;
using Contracts;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace OrderService.ComponentTests;

[Collection(nameof(OrderCollection))]
public class PlaceOrderTests(OrderApiFactory factory) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Places_an_order_when_stock_and_payment_both_succeed()
    {
        var ct = TestContext.Current.CancellationToken;

        factory.StubStock("SKU-COFFEE", availableQuantity: 120, unitPriceCents: 899);
        factory.StubPaymentAuthorised();

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/orders", new PlaceOrderRequest("cust-1", [new PlaceOrderItem("SKU-COFFEE", 2)]), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(ct);

        order.ShouldNotBeNull();
        order.Status.ShouldBe(OrderStatus.Placed);
        order.TotalCents.ShouldBe(1_798);
        order.Lines.Single().UnitPriceCents.ShouldBe(899);

        // Scoped to this order rather than "any OrderPlaced", so the assertion cannot be
        // satisfied by a message an earlier test in the collection published.
        var harness = factory.Services.GetRequiredService<ITestHarness>();
        harness.Published.Select<OrderPlaced>(ct)
            .Any(m => m.Context.Message.OrderId == order.OrderId)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Rejects_an_order_for_a_sku_inventory_does_not_know()
    {
        var ct = TestContext.Current.CancellationToken;

        factory.StubStockNotFound("SKU-NOPE");

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/orders", new PlaceOrderRequest("cust-1", [new PlaceOrderItem("SKU-NOPE", 1)]), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadAsStringAsync(ct);
        problem.ShouldContain("Unknown SKU");

        factory.Payments.LogEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Conflicts_when_inventory_holds_less_than_the_requested_quantity()
    {
        var ct = TestContext.Current.CancellationToken;

        factory.StubStock("SKU-GRINDER", availableQuantity: 3, unitPriceCents: 6_400);

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/orders", new PlaceOrderRequest("cust-1", [new PlaceOrderItem("SKU-GRINDER", 10)]), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadAsStringAsync(ct);
        problem.ShouldContain("3 available");

        // Stock is checked before payment, so the customer is never charged.
        factory.Payments.LogEntries.ShouldBeEmpty();
    }
}
