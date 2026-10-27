using System.ComponentModel.DataAnnotations;
using ContosoInventory.Server.Data;
using ContosoInventory.Server.Models;
using ContosoInventory.Shared.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ContosoInventory.Server.Services;

/// <summary>
/// Manages products using the inventory database.
/// </summary>
public class ProductService : IProductService
{
    private readonly InventoryContext _context;
    private readonly ILogger<ProductService> _logger;

    /// <summary>Creates the service with its database and logger.</summary>
    public ProductService(InventoryContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ProductResponseDto>> GetAllProductsAsync(int? categoryId = null)
    {
        try
        {
            if (categoryId.HasValue)
            {
                ValidateId(categoryId.Value);
            }

            var query = _context.Products.AsNoTracking();
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            var products = await query.OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync();
            return products.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products for category {CategoryId}.", categoryId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProductResponseDto?> GetProductByIdAsync(int id)
    {
        try
        {
            ValidateId(id);
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            return product == null ? null : MapToDto(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}.", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProductResponseDto> CreateProductAsync(CreateProductDto dto)
    {
        try
        {
            ValidateDto(dto);
            var sku = dto.Sku.Trim();
            var normalizedSku = sku.ToUpperInvariant();
            await ValidateReferencesAsync(dto.CategoryId, normalizedSku);

            var now = DateTime.UtcNow;
            var product = new Product
            {
                Name = dto.Name.Trim(),
                Sku = sku,
                NormalizedSku = normalizedSku,
                Description = dto.Description,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                CategoryId = dto.CategoryId,
                CreatedDate = now,
                LastUpdatedDate = now
            };

            _context.Products.Add(product);
            await SaveProductChangesAsync();
            _logger.LogInformation("Product created: {Sku} (ID: {ProductId}).", product.Sku, product.Id);
            return MapToDto(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProductResponseDto?> UpdateProductAsync(int id, UpdateProductDto dto)
    {
        try
        {
            ValidateId(id);
            ValidateDto(dto);
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return null;
            }

            var sku = dto.Sku.Trim();
            var normalizedSku = sku.ToUpperInvariant();
            await ValidateReferencesAsync(dto.CategoryId, normalizedSku, id);

            product.Name = dto.Name.Trim();
            product.Sku = sku;
            product.NormalizedSku = normalizedSku;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.StockQuantity = dto.StockQuantity;
            product.CategoryId = dto.CategoryId;
            product.LastUpdatedDate = DateTime.UtcNow;
            await SaveProductChangesAsync();

            _logger.LogInformation("Product updated: {ProductId}.", id);
            return MapToDto(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}.", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProductAsync(int id)
    {
        try
        {
            ValidateId(id);
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return false;
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Product deleted: {ProductId}.", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}.", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProductResponseDto?> RestockProductAsync(int id, RestockProductDto dto)
    {
        try
        {
            ValidateId(id);
            ValidateDto(dto);
            var now = DateTime.UtcNow;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var affected = await _context.Products
                .Where(p => p.Id == id && p.StockQuantity <= int.MaxValue - dto.Quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity + dto.Quantity)
                    .SetProperty(p => p.LastUpdatedDate, now));

            if (affected == 0)
            {
                if (await _context.Products.AnyAsync(p => p.Id == id))
                {
                    throw new InvalidOperationException("Restocking would exceed the maximum stock quantity.");
                }

                return null;
            }

            var product = await _context.Products.AsNoTracking().SingleAsync(p => p.Id == id);
            await transaction.CommitAsync();
            _logger.LogInformation("Product {ProductId} restocked by {Quantity}.", id, dto.Quantity);
            return MapToDto(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restocking product {ProductId}.", id);
            throw;
        }
    }

    private async Task ValidateReferencesAsync(int categoryId, string normalizedSku, int? excludedId = null)
    {
        if (!await _context.Categories.AnyAsync(c => c.Id == categoryId))
        {
            throw new InvalidOperationException("The specified category does not exist.");
        }

        if (await _context.Products.AnyAsync(p => p.NormalizedSku == normalizedSku && p.Id != excludedId))
        {
            throw new InvalidOperationException("A product with this SKU already exists.");
        }
    }

    private async Task SaveProductChangesAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            throw new InvalidOperationException("A product with this SKU already exists.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 787 })
        {
            throw new InvalidOperationException("The specified category does not exist.", ex);
        }
    }

    private static void ValidateDto(object dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true))
        {
            throw new InvalidOperationException(string.Join(" ", results.Select(r => r.ErrorMessage)));
        }
    }

    private static void ValidateId(int id)
    {
        if (id <= 0)
        {
            throw new InvalidOperationException("Identifiers must be positive.");
        }
    }

    private static ProductResponseDto MapToDto(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Sku = product.Sku,
        Description = product.Description,
        Price = product.Price,
        StockQuantity = product.StockQuantity,
        CategoryId = product.CategoryId,
        CreatedDate = product.CreatedDate,
        LastUpdatedDate = product.LastUpdatedDate
    };
}
