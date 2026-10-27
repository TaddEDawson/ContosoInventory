namespace ContosoInventory.Shared.DTOs;

/// <summary>
/// Specifies the quantity to add to a product's stock.
/// </summary>
public class RestockProductDto
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
