# Repository guidance

## Build and run

All three projects target .NET 8. Run these PowerShell commands from the repository root:

```powershell
dotnet build .\ContosoInventory\ContosoInventory.sln
```

To start the application:

```powershell
Set-Location .\ContosoInventory\ContosoInventory.Server
dotnet run
```

The server hosts both the API and the Blazor client at `http://localhost:5240`.
Swagger is available at `/swagger` in Development. See [README.md](../README.md)
for prerequisites, demo credentials, and role permissions.

## Architecture

- The solution is nested under `ContosoInventory/`; the repository root is not
  the solution directory.
- `ContosoInventory.Client` is a Blazor WebAssembly SPA. Its scoped `HttpClient`
  uses the host's base address, so API requests go to the same server that serves
  the client. `CategoryApiService` centralizes category HTTP operations;
  `CookieAuthStateProvider` manages the client authentication state.
- `ContosoInventory.Server` serves static Blazor assets and falls back to
  `index.html` for client routes. API controllers delegate category operations
  through `ICategoryService` to `CategoryService`, which uses EF Core.
- `ContosoInventory.Shared` contains request/response DTOs with validation
  annotations, shared by the client and server. Persistence entities remain
  server-side.
- `InventoryContext` inherits from `IdentityDbContext<IdentityUser>`: inventory
  and Identity tables share the SQLite database. The default database is
  `App_Data/ContosoInventory.db` under the server project. Startup calls
  `DbInitializer.InitializeAsync` to apply migrations and seed demo data.
- This is a training starter application. Categories are implemented; Product
  management is an extension exercise, not an existing feature. Consult the
  [stakeholder specification](../stakeholders-starter-app-specification.md)
  for feature requirements, distinguishing planned behavior from current code.

## Cross-layer conventions

- Extend category-like features across shared DTOs, server entities/context and
  migrations, service interface/implementation, controller, client API service,
  and Razor UI. Register new services in the appropriate project's `Program.cs`.
- Controllers use `api/[controller]` routes. Authenticated users can read
  categories; mutations require `[Authorize(Roles = "Admin")]`. UI role checks
  complement, rather than replace, controller authorization.
- Authentication uses ASP.NET Core Identity cookies, not bearer tokens.
  `CookieAuthStateProvider` calls `/api/auth/login`, `/api/auth/logout`, and
  `/api/auth/me`, caching user information and notifying Blazor when it changes.
- Category services return shared response DTOs, not EF entities. Reads use
  `AsNoTracking`; category lists are ordered by `DisplayOrder`. Name duplicate
  checks are case-insensitive, and timestamps use UTC.
- Follow the category API's result mapping: missing entities produce 404,
  duplicate-name `InvalidOperationException`s become 400, creation returns
  `CreatedAtAction`, and successful deletion returns 204. Service failures are
  logged and rethrown rather than converted to successful results.
- Keep the HTTP-only localhost training setup distinct from production
  deployment. Server `Program.cs` documents the coupled HTTPS, cookie, and CORS
  changes; authentication also relies on strict same-site cookies.
