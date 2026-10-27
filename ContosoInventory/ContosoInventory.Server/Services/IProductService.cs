using ContosoInventory.Shared.DTOs;

namespace ContosoInventory.Server.Services;

/// <summary>
/// Defines product CRUD and stock operations. Invalid input, duplicate SKUs and
/// missing categories produce InvalidOperationException.
/// </summary>
public interface IProductService
{
    /// <summary>Returns products ordered by name, optionally filtered by category.</summary>
    /// <param name="categoryId">A positive category ID, or null for all products.</param>
    /// <returns>The matching products.</returns>
    Task<List<ProductResponseDto>> GetAllProductsAsync(int? categoryId = null);

    /// <summary>Finds a product by ID.</summary>
    /// <param name="id">A positive product ID.</param>
    /// <returns>The product, or null if missing.</returns>
    Task<ProductResponseDto?> GetProductByIdAsync(int id);

    /// <summary>Creates a product in an existing category with a unique trimmed SKU.</summary>
    /// <param name="dto">The validated product fields.</param>
    /// <returns>The created product with UTC timestamps.</returns>
    Task<ProductResponseDto> CreateProductAsync(CreateProductDto dto);

    /// <summary>Replaces all writable fields, including stock, and updates the UTC timestamp.</summary>
    /// <param name="id">A positive product ID.</param>
    /// <param name="dto">The replacement product fields.</param>
    /// <returns>The updated product, or null if missing.</returns>
    Task<ProductResponseDto?> UpdateProductAsync(int id, UpdateProductDto dto);

    /// <summary>Deletes a product.</summary>
    /// <param name="id">A positive product ID.</param>
    /// <returns>True when deleted, or false if missing.</returns>
    Task<bool> DeleteProductAsync(int id);

    /// <summary>Atomically adds a positive quantity, rejecting integer overflow.</summary>
    /// <param name="id">A positive product ID.</param>
    /// <param name="dto">The quantity to add.</param>
    /// <returns>The updated product, or null if missing.</returns>
    Task<ProductResponseDto?> RestockProductAsync(int id, RestockProductDto dto);
}
