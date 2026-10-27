using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ContosoInventory.Server.Models;

namespace ContosoInventory.Server.Data;

public class InventoryContext : IdentityDbContext<IdentityUser>
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;

    public InventoryContext(DbContextOptions<InventoryContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).IsRequired().HasMaxLength(500);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Sku).IsRequired().HasMaxLength(50);
            entity.Property(p => p.NormalizedSku).IsRequired().HasMaxLength(50);
            entity.HasIndex(p => p.NormalizedSku).IsUnique();
            entity.Property(p => p.Description).IsRequired().HasMaxLength(1000);
            entity.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(p => p.CreatedDate)
                .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
            entity.Property(p => p.LastUpdatedDate)
                .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
            entity.ToTable("Products", table =>
                table.HasCheckConstraint("CK_Products_StockQuantity", "\"StockQuantity\" >= 0 AND \"StockQuantity\" <= 2147483647"));
        });
    }
}
