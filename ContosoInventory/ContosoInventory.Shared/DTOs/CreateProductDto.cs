namespace ContosoInventory.Shared.DTOs;

/// <summary>
/// Contains the writable product fields.
/// </summary>
public class CreateProductDto : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    /// <summary>
    /// Rejects prices with more than two meaningful decimal places.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Price != decimal.Round(Price, 2))
        {
            yield return new ValidationResult(
                "Price must have at most two decimal places.", new[] { nameof(Price) });
        }
    }
}
