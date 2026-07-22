using Microsoft.EntityFrameworkCore;

namespace PaymentService;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("payments");
            payment.HasKey(x => x.Id);
            payment.Property(x => x.Currency).HasMaxLength(3);
            payment.Property(x => x.Status).HasMaxLength(16);
            payment.HasIndex(x => x.OrderId);
        });
    }
}
