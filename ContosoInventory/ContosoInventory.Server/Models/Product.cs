namespace ContosoInventory.Server.Models;

/// <summary>
/// Represents inventory assigned to a category.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Stores the invariant-uppercase SKU for case-insensitive uniqueness.
    /// </summary>
    public string NormalizedSku { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
}
