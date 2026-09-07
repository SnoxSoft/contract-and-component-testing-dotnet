using System.Net.Http.Json;
using System.Text.Json;
using Polly.Timeout;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace OrderService.ComponentTests;

[Collection(nameof(OrderCollection))]
public class OutageTests(OrderApiFactory factory) : IAsyncLifetime
{
    private const int ExpectedAttempts = 4; // one call plus three retries

    public ValueTask InitializeAsync() => factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<HttpResponseMessage> PlaceOrder(string sku, CancellationToken ct) =>
        factory.CreateClient().PostAsJsonAsync(
            "/orders", new PlaceOrderRequest("cust-1", [new PlaceOrderItem(sku, 1)]), ct);

    // A request is only written to the log once the stub has finished serving it, so an
    // attempt abandoned by the per-attempt timeout can be recorded after the test that
    // caused it has moved on. Counting the whole log would then attribute it to whichever
    // test runs next. Matching on the path, and giving every test its own SKU, keeps each
    // count to the requests that test actually caused.
    private int StockCalls(string sku) =>
        factory.Inventory.FindLogEntries(Request.Create().WithPath($"/stock/{sku}").UsingGet()).Count;

    private int PaymentCalls() =>
        factory.Payments.FindLogEntries(Request.Create().WithPath("/payments").UsingPost()).Count;

    [Fact]
    public async Task Retries_inventory_before_giving_up_on_a_503()
    {
        var ct = TestContext.Current.CancellationToken;
        const string sku = "SKU-OUTAGE-503";

        factory.Inventory
            .Given(Request.Create().WithPath($"/stock/{sku}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        await Should.ThrowAsync<HttpRequestException>(() => PlaceOrder(sku, ct));

        StockCalls(sku).ShouldBe(ExpectedAttempts);
        PaymentCalls().ShouldBe(0);
    }

    [Fact]
    public async Task Retries_a_500_the_same_way_as_a_503()
    {
        var ct = TestContext.Current.CancellationToken;
        const string sku = "SKU-OUTAGE-500";

        factory.Inventory
            .Given(Request.Create().WithPath($"/stock/{sku}").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        await Should.ThrowAsync<HttpRequestException>(() => PlaceOrder(sku, ct));

        StockCalls(sku).ShouldBe(ExpectedAttempts);
    }

    /// <summary>
    /// Characterises current behaviour: the standard resilience handler retries by status
    /// code without regard for the HTTP verb, so a payment attempt is sent four times.
    /// If this test starts failing, the retry policy was made verb-aware and that is a fix,
    /// not a regression.
    /// </summary>
    [Fact]
    public async Task Retries_the_payment_call_even_though_posting_a_payment_is_not_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        const string sku = "SKU-OUTAGE-PAYMENT";

        factory.StubStock(sku, availableQuantity: 120, unitPriceCents: 899);
        factory.Payments
            .Given(Request.Create().WithPath("/payments").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        await Should.ThrowAsync<HttpRequestException>(() => PlaceOrder(sku, ct));

        PaymentCalls().ShouldBe(ExpectedAttempts);
    }

    [Fact]
    public async Task Gives_up_when_inventory_answers_too_slowly()
    {
        var ct = TestContext.Current.CancellationToken;
        const string sku = "SKU-OUTAGE-SLOW";

        factory.Inventory
            .Given(Request.Create().WithPath($"/stock/{sku}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { sku, availableQuantity = 5, unitPriceCents = 899 })
                .WithDelay(TimeSpan.FromSeconds(3)));

        // The per-attempt timeout is one second, so a three second response never lands.
        await Should.ThrowAsync<TimeoutRejectedException>(() => PlaceOrder(sku, ct));

        PaymentCalls().ShouldBe(0);
    }

    [Fact]
    public async Task Does_not_retry_a_malformed_response()
    {
        var ct = TestContext.Current.CancellationToken;
        const string sku = "SKU-OUTAGE-MALFORMED";

        factory.Inventory
            .Given(Request.Create().WithPath($"/stock/{sku}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{ this is not json"));

        await Should.ThrowAsync<JsonException>(() => PlaceOrder(sku, ct));

        // A malformed body is not a transient fault, so retrying it would only waste time.
        StockCalls(sku).ShouldBe(1);
    }
}
