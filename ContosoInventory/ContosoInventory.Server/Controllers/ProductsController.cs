using System.ComponentModel.DataAnnotations;
using ContosoInventory.Server.Services;
using ContosoInventory.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContosoInventory.Server.Controllers;

/// <summary>
/// Manages products and stock; only administrators may mutate inventory.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    /// <summary>Creates a controller using the product service.</summary>
    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Returns all products, optionally filtered by category.</summary>
    /// <param name="categoryId">An optional positive category identifier.</param>
    /// <returns>The matching products ordered by name.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProductResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllProducts([FromQuery, Range(1, int.MaxValue)] int? categoryId = null)
    {
        var products = await _productService.GetAllProductsAsync(categoryId);
        return Ok(products);
    }

    /// <summary>Returns a product by ID.</summary>
    /// <param name="id">A positive product identifier.</param>
    /// <returns>The product, or 404 if missing.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById([FromRoute, Range(1, int.MaxValue)] int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    /// <summary>Creates a product.</summary>
    /// <param name="dto">The product fields.</param>
    /// <returns>The created product and its Location header.</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
    {
        try
        {
            var product = await _productService.CreateProductAsync(dto);
            return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Replaces a product, including its absolute stock quantity.</summary>
    /// <param name="id">A positive product identifier.</param>
    /// <param name="dto">All writable product fields.</param>
    /// <returns>The updated product, or 404 if missing.</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(
        [FromRoute, Range(1, int.MaxValue)] int id, [FromBody] UpdateProductDto dto)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(id, dto);
            return product == null ? NotFound() : Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Deletes a product.</summary>
    /// <param name="id">A positive product identifier.</param>
    /// <returns>No content on success, or 404 if missing.</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct([FromRoute, Range(1, int.MaxValue)] int id)
    {
        var deleted = await _productService.DeleteProductAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Adds a positive quantity to a product's stock.</summary>
    /// <param name="id">A positive product identifier.</param>
    /// <param name="dto">The quantity to add.</param>
    /// <returns>The updated product, or 404 if missing.</returns>
    [HttpPost("{id}/restock")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestockProduct(
        [FromRoute, Range(1, int.MaxValue)] int id, [FromBody] RestockProductDto dto)
    {
        try
        {
            var product = await _productService.RestockProductAsync(id, dto);
            return product == null ? NotFound() : Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
