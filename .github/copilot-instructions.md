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
  server-side. If a request DTO fails validation, return 400 Bad Request with
  a JSON body containing the validation error messages. Do not return 500 for
  validation failures.
- `InventoryContext` inherits from `IdentityDbContext<IdentityUser>`: inventory
  and Identity tables share the SQLite database. The default database is
  `App_Data/ContosoInventory.db` under the server project. Startup calls
  `DbInitializer.InitializeAsync` to apply migrations and seed demo data.
- This is a training starter application. Categories are implemented; Product
  management is an extension exercise, not an existing feature. Consult the
  [stakeholder specification](../stakeholders-starter-app-specification.md)
  for feature requirements. When describing features from the specification,
  label each item as 'Implemented' (present in the current code) or 'Planned'
  (in the specification only). Do not describe Planned items as existing behavior.

## Cross-layer conventions

- Extend category-like features across shared DTOs, server entities/context and
  migrations, service interface/implementation, controller, client API service,
  and Razor UI. Register new services in the appropriate project's `Program.cs`.
- Controllers use `api/[controller]` routes. Authenticated users can read
  categories; mutations require `[Authorize(Roles = "Admin")]`. UI role checks
  complement, rather than replace, controller authorization. Unauthenticated
  requests to protected endpoints return 401 Unauthorized. Authenticated users
  without the Admin role return 403 Forbidden for mutation endpoints.
- Authentication uses ASP.NET Core Identity cookies, not bearer tokens.
  `CookieAuthStateProvider` calls `/api/auth/login`, `/api/auth/logout`, and
  `/api/auth/me`, caching user information and notifying Blazor when it changes.
- Category services return shared response DTOs, not EF entities. Reads use
  `AsNoTracking`; category lists are ordered by `DisplayOrder`. Name duplicate
  checks are case-insensitive, and timestamps use UTC.
- Follow the category API's result mapping: missing entities produce 404,
  duplicate-name `InvalidOperationException`s become 400, creation returns
  201 via `CreatedAtAction`, successful updates (PUT or PATCH) return 200 OK
  with the updated response DTO, and successful deletion returns 204. Missing
  entities return 404 for all mutation operations. Service failures are logged
  and rethrown rather than converted to successful results. Controllers map
  unhandled service exceptions to HTTP 500 with the JSON body
  `{ "message": "An unexpected error occurred." }`. Do not include stack traces
  or exception details in the response.
- When changing server code, keep the existing HTTP localhost settings in
  server `Program.cs` unchanged unless the user explicitly asks for production
  configuration. Do not enable HTTPS-only, Secure cookie, or production CORS
  settings by default. Server `Program.cs` documents the coupled HTTPS, cookie,
  and CORS changes; authentication also relies on strict same-site cookies.
 # Contoso Inventory API - Coding Standards

 ## Naming Conventions
 - Use PascalCase for class names, public methods, and public properties.
 - Use camelCase for local variables and method parameters.
 - Prefix private fields with an underscore (e.g., _inventoryService).
 - Prefix interface names with "I" (e.g., IInventoryService).

 ## Architecture Patterns
 - Data access goes through EF Core `InventoryContext` inside service classes. Do not create repository interfaces or classes unless the user explicitly requests them.
 - Use dependency injection for all service dependencies. Register services in Program.cs.
 - Separate business logic into service classes. Controllers should only handle HTTP concerns.
 - Use DTOs (Data Transfer Objects) for API request and response payloads. Never expose database entities directly.

 ## Error Handling
 - Use try-catch blocks for all external API calls and database operations.
 - Return appropriate HTTP status codes: 200 for successful reads and updates (updates include the updated response DTO), 201 via `CreatedAtAction` for creation, 204 for successful deletion, 400 for bad requests, 401 for unauthenticated requests to protected endpoints, 403 for authenticated users lacking the required role, 404 for not found, and 500 for unhandled server errors.
 - Log all exceptions using ILogger<T> with structured logging.
 - Include meaningful error messages in API responses. For unhandled service exceptions, use only `{ "message": "An unexpected error occurred." }` and never expose stack traces or exception details.

 ## Documentation
 - Include XML documentation comments on all public methods and classes.
 - Use inline comments only for complex business logic that is not self-explanatory.

 ## Testing
 - Write unit tests using xUnit and Moq.
 - Follow the Arrange-Act-Assert pattern in test methods.
 - Name test methods using the pattern: MethodName_Scenario_ExpectedResult.