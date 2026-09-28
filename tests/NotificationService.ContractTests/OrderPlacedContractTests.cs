using System.Text.Json;
using Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NotificationService;
using PactNet;
using PactNet.Matchers;
using Shouldly;

namespace NotificationService.ContractTests;

public class OrderPlacedContractTests
{
    private readonly IMessagePactBuilderV3 _pact = Pact
        .V3("NotificationService", "OrderService", new PactConfig
        {
            PactDir = Path.Combine("..", "..", "..", "..", "..", "pacts"),
            // MassTransit serialises with camelCase, so the pact must be read and written
            // the same way or the recorded message is not the one the broker carries.
            DefaultJsonSettings = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            }
        })
        .WithMessageInteractions();

    [Fact]
    public async Task Notifies_the_customer_about_an_order_that_was_placed()
    {
        await _pact
            .ExpectsToReceive("an order placed event")
                .Given("an order has been placed")
                .WithJsonContent(new
                {
                    orderId = Match.Type(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                    customerId = Match.Type("cust-1"),
                    totalCents = Match.Integer(3_048),
                    currency = Match.Type("EUR"),
                    placedAt = Match.Type(DateTimeOffset.Parse("2026-09-01T08:00:00Z")),
                    lines = Match.MinType(new
                    {
                        sku = Match.Type("SKU-COFFEE"),
                        quantity = Match.Integer(2),
                        unitPriceCents = Match.Integer(899)
                    }, 1)
                })
            .VerifyAsync<OrderPlaced>(async message =>
            {
                // The real consumer, driven through a real MassTransit pipeline, so the
                // message goes through the same deserialisation it would in production.
                await using var provider = new ServiceCollection()
                    .AddSingleton<INotificationSink, InMemoryNotificationSink>()
                    .AddMassTransitTestHarness(bus => bus.AddConsumer<OrderPlacedConsumer>())
                    .BuildServiceProvider(validateScopes: true);

                var harness = provider.GetRequiredService<ITestHarness>();
                await harness.Start();

                await harness.Bus.Publish(message);

                (await harness.Consumed.Any<OrderPlaced>()).ShouldBeTrue();

                var sink = (InMemoryNotificationSink)provider.GetRequiredService<INotificationSink>();
                var notification = sink.Sent.ShouldHaveSingleItem();

                notification.OrderId.ShouldBe(message.OrderId);
                notification.CustomerId.ShouldBe(message.CustomerId);
                notification.Message.ShouldContain("item(s)");
            });
    }
}
