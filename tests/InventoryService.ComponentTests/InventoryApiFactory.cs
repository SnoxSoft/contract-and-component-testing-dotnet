using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InventoryService.ComponentTests;

public class InventoryApiFactory : WebApplicationFactory<Program>
{
    // Each factory gets its own store, so test classes cannot see each other's data.
    private readonly string _databaseName = $"inventory-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // AddDbContext registers the UseNpgsql delegate as IDbContextOptionsConfiguration.
            // Leaving it in place would apply both providers to the same options and throw.
            services.RemoveAll<IDbContextOptionsConfiguration<InventoryDbContext>>();
            services.RemoveAll<DbContextOptions<InventoryDbContext>>();
            services.RemoveAll<InventoryDbContext>();

            services.AddDbContext<InventoryDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task SeedAsync(params StockItem[] items)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        db.StockItems.AddRange(items);
        await db.SaveChangesAsync();
    }
}
