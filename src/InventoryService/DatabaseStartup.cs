using Microsoft.EntityFrameworkCore;

namespace InventoryService;

public static class DatabaseStartup
{
    private static readonly StockItem[] SeedItems =
    [
        new() { Sku = "SKU-COFFEE", AvailableQuantity = 120, UnitPriceCents = 899 },
        new() { Sku = "SKU-MUG", AvailableQuantity = 40, UnitPriceCents = 1250 },
        new() { Sku = "SKU-GRINDER", AvailableQuantity = 3, UnitPriceCents = 6400 }
    ];

    public static async Task MigrateAndSeedAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await db.Database.MigrateAsync();

        if (await db.StockItems.AnyAsync())
        {
            return;
        }

        db.StockItems.AddRange(SeedItems);
        await db.SaveChangesAsync();
    }
}
