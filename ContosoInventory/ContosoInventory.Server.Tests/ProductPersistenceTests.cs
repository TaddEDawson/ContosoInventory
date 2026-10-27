using System.ComponentModel.DataAnnotations;
using ContosoInventory.Server.Data;
using ContosoInventory.Server.Models;
using ContosoInventory.Server.Services;
using ContosoInventory.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ContosoInventory.Server.Tests;

public class ProductPersistenceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private InventoryContext _context = null!;
    private ProductService _service = null!;
    private int _categoryId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _context = new InventoryContext(new DbContextOptionsBuilder<InventoryContext>()
            .UseSqlite(_connection).Options);
        await _context.Database.MigrateAsync();
        var category = new Category { Name = "Hardware", Description = "Devices", DisplayOrder = 1 };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        _categoryId = category.Id;
        _service = new ProductService(_context, Mock.Of<ILogger<ProductService>>());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1.23", true)]
    [InlineData("1.2300", true)]
    [InlineData("79228162514264337593543950335", true)]
    [InlineData("-0.01", false)]
    [InlineData("0.001", false)]
    public void ProductDto_PriceValidation_EnforcesRangeAndScale(string price, bool expectedValid)
    {
        var dto = NewProduct();
        dto.Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        Assert.Equal(expectedValid, valid);
    }

    [Fact]
    public async Task CreateProduct_ValidBoundaryValues_RoundTripThroughSqlite()
    {
        var dto = NewProduct();
        dto.Name = new string('n', 200);
        dto.Sku = new string('s', 50);
        dto.Description = new string('d', 1000);
        dto.Price = decimal.MaxValue;
        dto.StockQuantity = int.MaxValue;
        var created = await _service.CreateProductAsync(dto);
        var read = await _service.GetProductByIdAsync(created.Id);
        Assert.Equal(dto.Name, read!.Name);
        Assert.Equal(dto.Sku, read.Sku);
        Assert.Equal(dto.Description, read.Description);
        Assert.Equal(dto.Price, read.Price);
        Assert.Equal(dto.StockQuantity, read.StockQuantity);
        Assert.Equal(DateTimeKind.Utc, read.CreatedDate.Kind);
        Assert.Equal(DateTimeKind.Utc, read.LastUpdatedDate.Kind);
    }

    [Fact]
    public async Task CreateProduct_ZeroPriceStockAndEmptyDescription_AreValid()
    {
        var dto = NewProduct();
        dto.Price = 0;
        dto.StockQuantity = 0;
        dto.Description = string.Empty;
        var created = await _service.CreateProductAsync(dto);
        Assert.Equal(0m, created.Price);
        Assert.Equal(0, created.StockQuantity);
        Assert.Empty(created.Description);
    }

    [Fact]
    public async Task CreateProduct_InactiveCategory_IsAllowed()
    {
        var category = await _context.Categories.SingleAsync();
        category.IsActive = false;
        await _context.SaveChangesAsync();
        var created = await _service.CreateProductAsync(NewProduct());
        Assert.Equal(_categoryId, created.CategoryId);
    }

    [Fact]
    public async Task ProductService_InvalidDirectInputs_AreRejectedWithoutWrites()
    {
        var dto = NewProduct();
        dto.Name = " ";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateProductAsync(dto));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetAllProductsAsync(0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetProductByIdAsync(0));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateProductAsync(0, new UpdateProductDto()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteProductAsync(0));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RestockProductAsync(1, new RestockProductDto { Quantity = 0 }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateProductAsync(null!));
        Assert.Empty(await _context.Products.ToListAsync());
    }

    [Fact]
    public async Task CreateProduct_UnicodeCaseVariantSku_IsRejected()
    {
        var dto = NewProduct();
        dto.Sku = "\u00e9quipement-1";
        await _service.CreateProductAsync(dto);
        dto.Sku = "  \u00c9QUIPEMENT-1  ";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateProductAsync(dto));
        Assert.Single(await _context.Products.ToListAsync());
    }

    [Fact]
    public async Task ProductSchema_DuplicateNormalizedSku_IsRejectedByDatabase()
    {
        var created = await _service.CreateProductAsync(NewProduct());
        _context.Products.Add(NewEntity(created.Sku));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        Assert.Equal(2067, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task ProductSchema_MissingCategory_IsRejectedByDatabase()
    {
        var product = NewEntity("SKU-2");
        product.CategoryId = int.MaxValue;
        _context.Products.Add(product);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        Assert.Equal(787, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task ProductSchema_CategoryDeletion_IsRestrictedByDatabase()
    {
        await _service.CreateProductAsync(NewProduct());
        _context.ChangeTracker.Clear();
        _context.Categories.Remove(await _context.Categories.SingleAsync());
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        Assert.Equal(787, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task ProductSchema_NegativeStock_IsRejectedByDatabase()
    {
        var product = NewEntity("SKU-3");
        product.StockQuantity = -1;
        _context.Products.Add(product);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        Assert.Equal(275, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task ProductMigration_UpgradeAndRollback_PreserveExistingCategoryAndIdentityData()
    {
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260218213551_InitialCreate");
        _context.Users.Add(new IdentityUser { UserName = "existing-user", NormalizedUserName = "EXISTING-USER" });
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();
        Assert.Equal("Hardware", (await _context.Categories.SingleAsync()).Name);
        Assert.Equal("existing-user", (await _context.Users.SingleAsync()).UserName);
        Assert.Empty(await _context.Products.ToListAsync());
        Assert.Empty(await _context.Database.GetPendingMigrationsAsync());

        await _service.CreateProductAsync(NewProduct());
        await migrator.MigrateAsync("20260218213551_InitialCreate");
        Assert.Single(await _context.Categories.ToListAsync());
        Assert.Single(await _context.Users.ToListAsync());
        await migrator.MigrateAsync();
        Assert.Empty(await _context.Products.AsNoTracking().ToListAsync());
    }

    private CreateProductDto NewProduct() => new()
    {
        Name = "Product",
        Sku = "Sku-1",
        Description = "Hardware",
        Price = 1.23m,
        StockQuantity = 10,
        CategoryId = _categoryId
    };

    private Product NewEntity(string sku) => new()
    {
        Name = "Product",
        Sku = sku,
        NormalizedSku = sku.ToUpperInvariant(),
        Description = "Hardware",
        CategoryId = _categoryId,
        CreatedDate = DateTime.UtcNow,
        LastUpdatedDate = DateTime.UtcNow
    };
}
