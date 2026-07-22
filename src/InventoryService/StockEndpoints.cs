using Microsoft.EntityFrameworkCore;

namespace InventoryService;

public record StockResponse(string Sku, int AvailableQuantity, int UnitPriceCents);

public static class StockEndpoints
{
    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/stock/{sku}", async (string sku, InventoryDbContext db, CancellationToken ct) =>
        {
            var item = await db.StockItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Sku == sku, ct);

            return item is null
                ? Results.Problem(
                    title: "Unknown SKU",
                    detail: $"No stock item exists with SKU '{sku}'.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(new StockResponse(item.Sku, item.AvailableQuantity, item.UnitPriceCents));
        });
    }
}
