using Microsoft.EntityFrameworkCore;

namespace InventoryService;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> StockItems => Set<StockItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(item =>
        {
            item.ToTable("stock_items");
            item.HasKey(x => x.Sku);
            item.Property(x => x.Sku).HasMaxLength(64);
        });
    }
}
