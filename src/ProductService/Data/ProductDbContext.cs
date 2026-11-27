using Microsoft.EntityFrameworkCore;
using ProductService.Models;

namespace ProductService.Data;

public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Price).HasPrecision(18, 2);

            e.HasData(
                new Product { Id = 1, Name = "Laptop", Price = 55000m, StockQuantity = 20 },
                new Product { Id = 2, Name = "Mouse", Price = 700m, StockQuantity = 100 });
        });
    }
}