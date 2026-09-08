using System.Net;
using OrderService.Clients;
using PactNet;
using PactNet.Matchers;
using Shouldly;

namespace OrderService.ContractTests;

public class InventoryClientContractTests
{
    private readonly IPactBuilderV4 _pact = Pact
        .V4("OrderService", "InventoryService", new PactConfig
        {
            PactDir = Path.Combine("..", "..", "..", "..", "..", "pacts")
        })
        .WithHttpInteractions();

    [Fact]
    public async Task Gets_stock_for_a_sku_the_provider_knows()
    {
        _pact
            .UponReceiving("a request for the stock level of a known SKU")
                .Given("stock exists for SKU-COFFEE")
                .WithRequest(HttpMethod.Get, "/stock/SKU-COFFEE")
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    sku = Match.Type("SKU-COFFEE"),
                    availableQuantity = Match.Integer(120),
                    unitPriceCents = Match.Integer(899)
                });

        await _pact.VerifyAsync(async ctx =>
        {
            var client = new InventoryClient(new HttpClient { BaseAddress = ctx.MockServerUri });

            var stock = await client.GetStockAsync("SKU-COFFEE");

            stock.ShouldNotBeNull();
            stock.Sku.ShouldBe("SKU-COFFEE");
            stock.AvailableQuantity.ShouldBe(120);
            stock.UnitPriceCents.ShouldBe(899);
        });
    }

    [Fact]
    public async Task Treats_an_unknown_sku_as_absent_rather_than_an_error()
    {
        _pact
            .UponReceiving("a request for the stock level of an unknown SKU")
                .Given("no stock exists for SKU-NOPE")
                .WithRequest(HttpMethod.Get, "/stock/SKU-NOPE")
            .WillRespond()
                .WithStatus(HttpStatusCode.NotFound);

        await _pact.VerifyAsync(async ctx =>
        {
            var client = new InventoryClient(new HttpClient { BaseAddress = ctx.MockServerUri });

            var stock = await client.GetStockAsync("SKU-NOPE");

            stock.ShouldBeNull();
        });
    }
}
