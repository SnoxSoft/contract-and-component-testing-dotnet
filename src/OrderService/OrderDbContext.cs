using Microsoft.EntityFrameworkCore;

namespace OrderService;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");
            order.HasKey(x => x.Id);
            order.Property(x => x.CustomerId).HasMaxLength(64);
            order.Property(x => x.Currency).HasMaxLength(3);
            order.Property(x => x.Status).HasMaxLength(32);
            order.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<OrderLine>(line =>
        {
            line.ToTable("order_lines");
            line.HasKey(x => x.Id);
            line.Property(x => x.Sku).HasMaxLength(64);
        });
    }
}
