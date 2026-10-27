# ContosoInventory

A C# web application for managing Contoso's IT equipment inventory. Built with ASP.NET Core Web API, Blazor WebAssembly, and Entity Framework Core with SQLite.

This starter application is designed for the Microsoft Learn training module **"Configure and customize GitHub Copilot in Visual Studio Code."** Categories are implemented in both the API and Blazor UI. Product inventory management is implemented in the API; a Product UI remains planned.

## Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Git 2.48](https://git-scm.com/downloads) or later

## Getting Started

1. Clone the repository:

    ```bash
    git clone https://github.com/MicrosoftLearning/github-copilot-customization-starter-app.git
    cd github-copilot-customization-starter-app/ContosoInventory
    ```

2. Build the application:

    ```bash
    cd ContosoInventory.Server
    dotnet build
    ```

3. Run the application:

    ```bash
    dotnet run
    ```

4. Open a browser and navigate to `http://localhost:5240`.

On the first run, the application automatically creates the SQLite database, applies migrations, and seeds demo data.

## Demo Credentials

| User | Email | Password | Role |
|---|---|---|---|
| Mateo Gomez | `mateo@contoso.com` | `Password123!` | Admin |
| Megan Bowen | `megan@contoso.com` | `Password123!` | Viewer |

- **Admin** users can create, edit, delete, and toggle categories.
- **Viewer** users have read-only access.

## Product Inventory API (Implemented)

All Product endpoints require cookie authentication through `/api/auth/login`.
Only Admin users may create, update, delete, or restock products.

| Method | Route | Result |
|---|---|---|
| GET | `/api/products?categoryId=1` | All products ordered by name, optionally filtered by category |
| GET | `/api/products/{id}` | A product by ID |
| POST | `/api/products` | Created product (`201` with a Location header) |
| PUT | `/api/products/{id}` | Updated product (`200`) |
| DELETE | `/api/products/{id}` | No content (`204`) |
| POST | `/api/products/{id}/restock` | Updated product (`200`) |

Create and update accept the same writable fields:

```json
{
  "name": "Business laptop",
  "sku": "LAPTOP-001",
  "description": "Employee workstation",
  "price": 1299.99,
  "stockQuantity": 10,
  "categoryId": 1
}
```

- Name and SKU must be nonblank, at most 200 and 50 characters respectively.
  Leading and trailing whitespace is trimmed. SKU uniqueness is case-insensitive
  using an internal invariant-uppercase key; the trimmed display SKU is preserved.
- Description may be empty (the default) but not null, and is limited to 1000 characters.
- Price must be nonnegative and have at most two meaningful decimal places.
  Stock must be between zero and `2147483647`. Omitted price and stock default to zero.
- CategoryId must reference an existing category; inactive categories are allowed.
  A category containing products cannot be deleted (`400`).
- PUT replaces all writable fields, including the absolute stock quantity.
  Concurrent replacements use last-writer-wins semantics.
- Restock accepts `{ "quantity": 5 }`. The quantity must be positive, and the total
  stock cannot exceed `2147483647`. Increments are atomic; concurrent restocks do
  not lose stock additions. Restock requests are not idempotent: retrying adds stock again.
- Responses contain Id, Name, Sku, Description, Price, StockQuantity, CategoryId,
  CreatedDate, and LastUpdatedDate. Timestamps are server-managed UTC.
- Missing products return `404`; invalid input and business-rule violations return
  `400`. Invalid model inputs use validation problem details; business errors use
  `{ "message": "..." }`. Unauthenticated requests return `401`, and non-Admin
  mutations return `403`. Unhandled errors return `500` with a generic JSON message.

The `AddProducts` EF Core migration adds the Products table, a category foreign key
with restricted deletion, a unique normalized-SKU index, a category index, and a
stock-range constraint. Startup applies migrations automatically to the existing
SQLite database. Back up non-disposable databases first; rolling back this migration
removes Product data, but leaves Category and Identity data intact.

## Tests

Run the xUnit suite from the repository root:

```powershell
dotnet test .\ContosoInventory\ContosoInventory.Server.Tests\ContosoInventory.Server.Tests.csproj
```

Tests use isolated SQLite databases, exercise the API with real Identity cookies,
and verify validation, role access, CRUD/filtering, concurrent restocks, database
constraints, and migration upgrade/rollback. The existing application database is
not used or modified.

## Project Structure

```
ContosoInventory/
├── ContosoInventory.Server/    ASP.NET Core Web API + EF Core + Identity
├── ContosoInventory.Client/    Blazor WebAssembly frontend
└── ContosoInventory.Shared/    Shared DTOs and models
```

- **Server**: REST API with controllers, services (repository pattern), SQLite database via EF Core, and ASP.NET Core Identity authentication.
- **Client**: Blazor WebAssembly SPA with Bootstrap 5 UI, authentication state management, and typed HTTP API clients.
- **Shared**: Data transfer objects (DTOs) with validation annotations, shared between server and client.

## API Documentation

When the application is running, Swagger UI is available at: `http://localhost:5240/swagger`

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
