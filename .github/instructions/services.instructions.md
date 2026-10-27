---
name: 'Service Layer Standards'
description: 'Coding standards for business logic service classes'
applyTo: '**/Services/**/*.cs'
---
# Service Layer Standards

- Define a corresponding interface for every service class (e.g., IProductService for ProductService).
- Use async/await for all I/O-bound operations (database calls, HTTP requests, file I/O).
- Accept and return DTOs, not database entities.
- Include comprehensive input validation at the start of each public method.
- Throw specific exception types (ArgumentException, InvalidOperationException) rather than generic Exception.
- Log significant operations using ILogger<T> with structured logging parameters.
- Keep service methods focused on a single responsibility; avoid mixing business logic with infrastructure concerns.
- Do not catch exceptions unless you can handle them meaningfully; let unhandled exceptions propagate to the global exception handler.
- Avoid direct dependencies on infrastructure components (e.g., DbContext, HttpClient) within service methods; use abstractions or inject them through the constructor.
- Ensure that service methods are unit-testable by keeping dependencies injectable and avoiding static calls or hard-coded infrastructure references.
- Prefer returning rich result types (e.g., OperationResult<T>) over using out parameters or relying solely on exceptions for flow control.
- Keep service methods cohesive and avoid side effects; a method should either perform an action or query data, but not both.
- Document the expected behavior, input parameters, and return values for each service method using XML comments.
- Avoid performing authorization checks within service methods; rely on controller-level authorization attributes to enforce access control.
- Ensure that service methods are resilient to invalid inputs and edge cases, and fail gracefully with meaningful exceptions or result types.
- Avoid performing I/O operations directly within service methods; delegate such tasks to injected abstractions to maintain testability and separation of concerns.
- Ensure that service methods do not depend on the order of execution of other service methods; each method should be independently callable and produce consistent results.
- Avoid side effects that impact global state or other services; any necessary state changes should be explicit and controlled within the method's scope.