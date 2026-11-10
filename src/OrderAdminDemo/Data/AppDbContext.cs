using Microsoft.EntityFrameworkCore;

namespace OrderAdminDemo.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PaymentNotification> PaymentNotifications => Set<PaymentNotification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(u => u.UserName).IsUnique();
        b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        b.Entity<Order>().HasIndex(o => o.OrderNo).IsUnique();
        b.Entity<Order>().HasIndex(o => o.MerchantTradeNo);
        b.Entity<Order>()
            .HasOne(o => o.Member).WithMany(m => m.Orders)
            .HasForeignKey(o => o.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Product>().Ignore(p => p.IsLowStock);
        b.Entity<OrderItem>().Ignore(i => i.Subtotal);
    }
}
