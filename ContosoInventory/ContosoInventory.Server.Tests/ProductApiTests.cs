using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContosoInventory.Server.Data;
using ContosoInventory.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ContosoInventory.Server.Tests;

public class ProductApiTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ProductApiTests(InventoryApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/products")]
    [InlineData("GET", "/api/products/1")]
    [InlineData("POST", "/api/products")]
    [InlineData("PUT", "/api/products/1")]
    [InlineData("DELETE", "/api/products/1")]
    [InlineData("POST", "/api/products/1/restock")]
    public async Task Products_AnonymousRequest_ReturnsUnauthorized(string method, string route)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/products")]
    [InlineData("PUT", "/api/products/1")]
    [InlineData("DELETE", "/api/products/1")]
    [InlineData("POST", "/api/products/1/restock")]
    public async Task Products_ViewerMutation_ReturnsForbidden(string method, string route)
    {
        using var client = await LoginAsync("megan@contoso.com");
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Products_AdminCrudAndViewerReads_PreserveFieldsAndFilter()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        using var viewer = await LoginAsync("megan@contoso.com");
        var dto = NewProduct();
        dto.Name = "  Workstation  ";
        dto.Sku = $"  {dto.Sku}  ";
        using var createdResponse = await admin.PostAsJsonAsync("/api/products", dto);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await ReadProductAsync(createdResponse);
        Assert.NotNull(createdResponse.Headers.Location);
        Assert.Equal($"/api/products/{created.Id}", createdResponse.Headers.Location.AbsolutePath, ignoreCase: true);
        Assert.Equal("Workstation", created.Name);
        Assert.Equal(dto.Sku.Trim(), created.Sku);
        Assert.Equal(dto.Price, created.Price);
        Assert.Equal(dto.StockQuantity, created.StockQuantity);
        Assert.Equal(dto.Description, created.Description);
        Assert.Equal(dto.CategoryId, created.CategoryId);
        Assert.Equal(created.CreatedDate, created.LastUpdatedDate);
        Assert.Equal(DateTimeKind.Utc, created.CreatedDate.Kind);

        using var read = await viewer.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var fromDatabase = await ReadProductAsync(read);
        Assert.Equal(created.CreatedDate, fromDatabase.CreatedDate);
        Assert.Equal(DateTimeKind.Utc, fromDatabase.CreatedDate.Kind);
        Assert.Equal(created.Price, fromDatabase.Price);
        using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(9, json.RootElement.EnumerateObject().Count());

        var all = await viewer.GetFromJsonAsync<List<ProductResponseDto>>("/api/products");
        Assert.Contains(all!, p => p.Id == created.Id);
        var filtered = await viewer.GetFromJsonAsync<List<ProductResponseDto>>(
            $"/api/products?categoryId={created.CategoryId}");
        Assert.Contains(filtered!, p => p.Id == created.Id);
        Assert.All(filtered!, p => Assert.Equal(created.CategoryId, p.CategoryId));
        var noMatches = await viewer.GetFromJsonAsync<List<ProductResponseDto>>(
            $"/api/products?categoryId={int.MaxValue}");
        Assert.Empty(noMatches!);

        var update = new UpdateProductDto
        {
            Name = "Updated workstation",
            Sku = created.Sku.ToLowerInvariant(),
            Description = "Changed",
            CategoryId = 2,
            Price = 100.01m,
            StockQuantity = 4
        };
        using var updatedResponse = await admin.PutAsJsonAsync($"/api/products/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await ReadProductAsync(updatedResponse);
        Assert.Equal(update.Name, updated.Name);
        Assert.Equal(update.Sku, updated.Sku);
        Assert.Equal(update.Description, updated.Description);
        Assert.Equal(update.Price, updated.Price);
        Assert.Equal(update.StockQuantity, updated.StockQuantity);
        Assert.Equal(update.CategoryId, updated.CategoryId);
        Assert.Equal(created.CreatedDate, updated.CreatedDate);
        Assert.True(updated.LastUpdatedDate >= created.LastUpdatedDate);

        using var restockedResponse = await admin.PostAsJsonAsync(
            $"/api/products/{created.Id}/restock", new RestockProductDto { Quantity = 6 });
        Assert.Equal(HttpStatusCode.OK, restockedResponse.StatusCode);
        var restocked = await ReadProductAsync(restockedResponse);
        Assert.Equal(10, restocked.StockQuantity);
        Assert.Equal(created.CreatedDate, restocked.CreatedDate);
        Assert.True(restocked.LastUpdatedDate >= updated.LastUpdatedDate);

        using var deleted = await admin.DeleteAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(string.Empty, await deleted.Content.ReadAsStringAsync());
        using var missing = await viewer.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("Name", " ")]
    [InlineData("Name", null)]
    [InlineData("Sku", "\t")]
    [InlineData("Sku", null)]
    [InlineData("Description", null)]
    [InlineData("Price", "-0.01")]
    [InlineData("Price", "1.001")]
    [InlineData("StockQuantity", "-1")]
    [InlineData("CategoryId", "0")]
    [InlineData("CategoryId", "2147483647")]
    [InlineData("NameLength", "201")]
    [InlineData("SkuLength", "51")]
    [InlineData("DescriptionLength", "1001")]
    public async Task Products_InvalidCreateOrUpdate_ReturnsBadRequest(string field, string? value)
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        var dto = NewProduct();
        using var createdResponse = await admin.PostAsJsonAsync("/api/products", dto);
        var created = await ReadProductAsync(createdResponse);
        var payload = JsonSerializer.SerializeToNode(dto)!.AsObject();
        if (field.EndsWith("Length"))
        {
            payload[field[..^6]] = new string('a', int.Parse(value!));
        }
        else if (field is "Price" or "StockQuantity" or "CategoryId")
        {
            payload[field] = JsonSerializer.SerializeToNode(decimal.Parse(
                value!, System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            payload[field] = value;
        }

        using var invalidCreate = await admin.PostAsJsonAsync("/api/products", payload);
        using var invalidUpdate = await admin.PutAsJsonAsync($"/api/products/{created.Id}", payload);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        var unchanged = await admin.GetFromJsonAsync<ProductResponseDto>($"/api/products/{created.Id}");
        Assert.Equal(dto.Price, unchanged!.Price);
        Assert.Equal(dto.Name, unchanged.Name);
        Assert.Equal(dto.StockQuantity, unchanged.StockQuantity);
        Assert.Equal(created.LastUpdatedDate, unchanged.LastUpdatedDate);
    }

    [Fact]
    public async Task Products_DuplicateSkuOnCreateAndUpdate_ReturnsBadRequest()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        var dto = NewProduct();
        using var first = await admin.PostAsJsonAsync("/api/products", dto);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var duplicate = NewProduct();
        duplicate.Sku = $"  {dto.Sku.ToLowerInvariant()}  ";
        using var rejectedCreate = await admin.PostAsJsonAsync("/api/products", duplicate);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedCreate.StatusCode);
        Assert.Contains("SKU", await rejectedCreate.Content.ReadAsStringAsync());

        var otherDto = NewProduct();
        using var otherResponse = await admin.PostAsJsonAsync("/api/products", otherDto);
        var other = await ReadProductAsync(otherResponse);
        using var rejectedUpdate = await admin.PutAsJsonAsync($"/api/products/{other.Id}", duplicate);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedUpdate.StatusCode);
    }

    [Fact]
    public async Task Products_ConcurrentDuplicateCreates_AllowOnlyOne()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        var dto = NewProduct();
        var results = await Task.WhenAll(
            admin.PostAsJsonAsync("/api/products", dto),
            admin.PostAsJsonAsync("/api/products", dto));
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.BadRequest);
        }
        finally
        {
            foreach (var response in results) response.Dispose();
        }
    }

    [Fact]
    public async Task Restock_ConcurrentRequests_DoNotLoseIncrements()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        var dto = NewProduct();
        using var createdResponse = await admin.PostAsJsonAsync("/api/products", dto);
        var created = await ReadProductAsync(createdResponse);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            admin.PostAsJsonAsync($"/api/products/{created.Id}/restock", new RestockProductDto { Quantity = 3 })));
        try
        {
            Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            var product = await admin.GetFromJsonAsync<ProductResponseDto>($"/api/products/{created.Id}");
            Assert.Equal(dto.StockQuantity + 24, product!.StockQuantity);
        }
        finally
        {
            foreach (var response in results) response.Dispose();
        }
    }

    [Fact]
    public async Task Restock_InvalidQuantityOrOverflow_ReturnsBadRequestWithoutChanges()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        var dto = NewProduct();
        dto.StockQuantity = int.MaxValue - 1;
        using var createdResponse = await admin.PostAsJsonAsync("/api/products", dto);
        var created = await ReadProductAsync(createdResponse);
        foreach (var quantity in new[] { 0, -1, 2, int.MaxValue })
        {
            using var rejected = await admin.PostAsJsonAsync(
                $"/api/products/{created.Id}/restock", new RestockProductDto { Quantity = quantity });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        using var missingQuantity = await admin.PostAsJsonAsync($"/api/products/{created.Id}/restock", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missingQuantity.StatusCode);
        var unchanged = await admin.GetFromJsonAsync<ProductResponseDto>($"/api/products/{created.Id}");
        Assert.Equal(dto.StockQuantity, unchanged!.StockQuantity);
        Assert.Equal(created.LastUpdatedDate, unchanged.LastUpdatedDate);
        using var atLimit = await admin.PostAsJsonAsync(
            $"/api/products/{created.Id}/restock", new RestockProductDto { Quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, atLimit.StatusCode);
        Assert.Equal(int.MaxValue, (await ReadProductAsync(atLimit)).StockQuantity);
    }

    [Fact]
    public async Task Products_MissingId_ReturnsNotFoundForAllOperations()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        using var read = await admin.GetAsync($"/api/products/{int.MaxValue}");
        using var update = await admin.PutAsJsonAsync($"/api/products/{int.MaxValue}", NewProduct());
        using var delete = await admin.DeleteAsync($"/api/products/{int.MaxValue}");
        using var restock = await admin.PostAsJsonAsync(
            $"/api/products/{int.MaxValue}/restock", new RestockProductDto { Quantity = 1 });
        Assert.All(new[] { read, update, delete, restock },
            response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
    }

    [Fact]
    public async Task Products_InvalidIdentifiers_ReturnValidationErrors()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        foreach (var route in new[] { "/api/products/0", "/api/products/-1", "/api/products/not-an-id", "/api/products?categoryId=0" })
        {
            using var response = await admin.GetAsync(route);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
            Assert.NotEmpty(problem!.Errors);
        }
    }

    [Fact]
    public async Task DeleteCategory_WithProduct_IsBlockedUntilProductDeleted()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        using var categoryResponse = await admin.PostAsJsonAsync("/api/categories", new CreateCategoryDto
        {
            Name = $"Category-{Guid.NewGuid():N}", Description = "Products test", DisplayOrder = 1
        });
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponseDto>();
        var dto = NewProduct();
        dto.CategoryId = category!.Id;
        using var createdResponse = await admin.PostAsJsonAsync("/api/products", dto);
        var product = await ReadProductAsync(createdResponse);
        using var blocked = await admin.DeleteAsync($"/api/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("contains products", await blocked.Content.ReadAsStringAsync());
        using var deletedProduct = await admin.DeleteAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deletedProduct.StatusCode);
        using var deletedCategory = await admin.DeleteAsync($"/api/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deletedCategory.StatusCode);
        using var missingCategory = await admin.DeleteAsync($"/api/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missingCategory.StatusCode);
    }

    [Fact]
    public async Task Products_UnhandledDatabaseFailure_ReturnsGenericJsonError()
    {
        using var admin = await LoginAsync("mateo@contoso.com");
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE Products RENAME TO ProductsUnavailable");
        try
        {
            using var response = await admin.GetAsync("/api/products");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.Single(error!);
            Assert.Equal("An unexpected error occurred.", error!["message"]);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE ProductsUnavailable RENAME TO Products");
        }
    }

    private HttpClient CreateClient() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginDto { Email = email, Password = "Password123!" });
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static CreateProductDto NewProduct() => new()
    {
        Name = "Test product",
        Sku = $"SKU-{Guid.NewGuid():N}",
        Description = "An inventory item",
        CategoryId = 1,
        Price = 19.99m,
        StockQuantity = 5
    };

    private static async Task<ProductResponseDto> ReadProductAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return Assert.IsType<ProductResponseDto>(await response.Content.ReadFromJsonAsync<ProductResponseDto>());
    }
}
