using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace InventoryService.ComponentTests;

public class StockEndpointTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    [Fact]
    public async Task Returns_the_stock_item_for_a_known_sku()
    {
        var ct = TestContext.Current.CancellationToken;

        await factory.SeedAsync(new StockItem
        {
            Sku = "SKU-TEST-TEA",
            AvailableQuantity = 7,
            UnitPriceCents = 450
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/stock/SKU-TEST-TEA", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stock = await response.Content.ReadFromJsonAsync<StockResponse>(ct);

        stock.ShouldNotBeNull();
        stock.Sku.ShouldBe("SKU-TEST-TEA");
        stock.AvailableQuantity.ShouldBe(7);
        stock.UnitPriceCents.ShouldBe(450);
    }
}
