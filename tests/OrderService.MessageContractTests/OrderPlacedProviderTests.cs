using System.Net.Http.Json;
using System.Text.Json;
using Contracts;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderService.ComponentTests;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;
using Shouldly;

namespace OrderService.MessageContractTests;

public class OrderPlacedProviderTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static readonly string PactPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pacts", "NotificationService-OrderService.json");

    // MassTransit puts camelCase on the wire, so the scenario must be serialised the
    // same way or every field looks missing to the verifier.
    private static readonly JsonSerializerOptions WireFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Honours_the_message_contract_published_by_NotificationService()
    {
        var published = await PlaceAnOrderAndCaptureTheEventAsync();

        using var verifier = new PactVerifier("OrderService", new PactVerifierConfig
        {
            Outputters = [new TestOutput()],
            LogLevel = PactLogLevel.Warn
        });

        verifier
            // Nothing listens on this port and nothing ever connects to it. The verifier
            // needs a primary transport registered before WithMessages will add the
            // message one; without it every interaction fails with
            // "builder error for url (message://...)". This pact has no HTTP
            // interactions, so the address is never used.
            .WithHttpEndpoint(new Uri("http://localhost:59999"))
            .WithMessages(scenarios => scenarios.Add("an order placed event", () => published), WireFormat)
            .WithFileSource(new FileInfo(PactPath))
            .Verify();
    }

    /// <summary>
    /// Places a real order through the real service and returns the event it actually
    /// published. Constructing an OrderPlaced here instead would verify nothing: the
    /// contract would be checked against a message this test invented.
    /// </summary>
    private async Task<OrderPlaced> PlaceAnOrderAndCaptureTheEventAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        await factory.ResetAsync();
        factory.StubStock("SKU-COFFEE", availableQuantity: 120, unitPriceCents: 899);
        factory.StubPaymentAuthorised();

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/orders", new PlaceOrderRequest("cust-1", [new PlaceOrderItem("SKU-COFFEE", 2)]), ct);

        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(ct);
        order.ShouldNotBeNull();

        var harness = factory.Services.GetRequiredService<ITestHarness>();

        return harness.Published.Select<OrderPlaced>(ct)
            .Select(m => m.Context.Message)
            .Single(m => m.OrderId == order.OrderId);
    }

    private sealed class TestOutput : IOutput
    {
        public void WriteLine(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);
    }
}
