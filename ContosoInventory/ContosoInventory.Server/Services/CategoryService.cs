using Microsoft.EntityFrameworkCore;
using ContosoInventory.Server.Data;
using ContosoInventory.Server.Models;
using ContosoInventory.Shared.DTOs;
using Microsoft.Data.Sqlite;

namespace ContosoInventory.Server.Services;

/// <summary>
/// Provides operations for managing inventory categories.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly InventoryContext _context;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(InventoryContext context, ILogger<CategoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<CategoryResponseDto>> GetAllCategoriesAsync()
    {
        try
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return categories.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all categories.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CategoryResponseDto?> GetCategoryByIdAsync(int id)
    {
        try
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            return category == null ? null : MapToDto(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category with ID {CategoryId}.", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CategoryResponseDto> CreateCategoryAsync(CreateCategoryDto dto)
    {
        try
        {
            // Check for duplicate name (case-insensitive)
            var exists = await _context.Categories
                .AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower());

            if (exists)
            {
                throw new InvalidOperationException($"A category with the name '{dto.Name}' already exists.");
            }

            var category = new Category
            {
                Name = dto.Name,
                Description = dto.Description,
                DisplayOrder = dto.DisplayOrder,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category created: {CategoryName} (ID: {CategoryId}).", category.Name, category.Id);

            return MapToDto(category);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category '{CategoryName}'.", dto.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CategoryResponseDto?> UpdateCategoryAsync(int id, UpdateCategoryDto dto)
    {
        try
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return null;
            }

            // Check for duplicate name (case-insensitive), excluding current category
            var exists = await _context.Categories
                .AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower() && c.Id != id);

            if (exists)
            {
                throw new InvalidOperationException($"A category with the name '{dto.Name}' already exists.");
            }

            category.Name = dto.Name;
            category.Description = dto.Description;
            category.DisplayOrder = dto.DisplayOrder;
            category.IsActive = dto.IsActive;
            category.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Category updated: {CategoryName} (ID: {CategoryId}).", category.Name, category.Id);

            return MapToDto(category);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category with ID {CategoryId}.", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteCategoryAsync(int id)
    {
        try
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return false;
            }

            if (await _context.Products.AnyAsync(p => p.CategoryId == id))
            {
                throw new InvalidOperationException("Cannot delete a category that contains products.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category deleted: {CategoryName} (ID: {CategoryId}).", category.Name, category.Id);

            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 787 or 1811 })
        {
            _logger.LogWarning(ex, "Category {CategoryId} contains products and cannot be deleted.", id);
            throw new InvalidOperationException("Cannot delete a category that contains products.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category with ID {CategoryId}.", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CategoryResponseDto?> ToggleActiveAsync(int id)
    {
        try
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return null;
            }

            category.IsActive = !category.IsActive;
            category.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Category toggled: {CategoryName} (ID: {CategoryId}) is now {Status}.",
                category.Name, category.Id, category.IsActive ? "active" : "inactive");

            return MapToDto(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling active status for category with ID {CategoryId}.", id);
            throw;
        }
    }

    private static CategoryResponseDto MapToDto(Category category)
    {
        return new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            CreatedDate = category.CreatedDate,
            LastModifiedDate = category.LastModifiedDate
        };
    }
}
