using IdealCreative.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartRecord> Carts => Set<CartRecord>();
    public DbSet<CouponRecord> Coupons => Set<CouponRecord>();
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();
    public DbSet<CategoryRecord> Categories => Set<CategoryRecord>();
    public DbSet<TagRecord> Tags => Set<TagRecord>();
    public DbSet<ReviewRecord> Reviews => Set<ReviewRecord>();
    public DbSet<AppSettingRecord> AppSettings => Set<AppSettingRecord>();
    public DbSet<PaymentTransactionRecord> PaymentTransactions => Set<PaymentTransactionRecord>();
    public DbSet<PrivacyRequestRecord> PrivacyRequests => Set<PrivacyRequestRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(180).IsRequired();
            entity.Property(product => product.Slug).HasMaxLength(180).IsRequired();
            entity.HasIndex(product => product.Slug).IsUnique();
            entity.Property(product => product.Description).HasMaxLength(10_000);
            entity.Property(product => product.PriceCents).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("ck_products_price_non_negative", "\"PriceCents\" >= 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_products_stock_non_negative", "\"Stock\" >= 0"));
        });
        builder.Entity<CartRecord>().HasKey(item => item.UserId);
        builder.Entity<CouponRecord>().HasKey(item => item.Code);
        builder.Entity<OrderRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.CreatedAt });
            entity.HasIndex(item => new { item.Status, item.CreatedAt });
        });
        builder.Entity<CategoryRecord>().HasKey(item => item.Id);
        builder.Entity<TagRecord>().HasKey(item => item.Id);
        builder.Entity<ReviewRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ProductId, item.CreatedAt });
        });
        builder.Entity<AppSettingRecord>().HasKey(item => item.Key);
        builder.Entity<PaymentTransactionRecord>().HasKey(item => item.Id);
        builder.Entity<PaymentTransactionRecord>().HasIndex(item => new { item.Provider, item.ProviderPaymentId }).IsUnique();
        builder.Entity<PrivacyRequestRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.Status });
        });
    }
}
