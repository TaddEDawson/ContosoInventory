---
name: 'API Controller Standards'
description: 'Coding standards for ASP.NET Core API controllers'
applyTo: '**/Controllers/**/*.cs'
---
# API Controller Standards

- Inherit from ControllerBase and apply the [ApiController] attribute.
- Use attribute routing with [Route("api/[controller]")] on the class.
- Limit each action to validating the route and request inputs, calling exactly one service method, and mapping its result to an HTTP response. Place all business logic, data access, and DTO-to-entity mapping in service classes.
- Use action-specific attributes: [HttpGet], [HttpPost], [HttpPut], [HttpDelete].
- Return IActionResult or ActionResult<T> from all action methods.
- Return NotFound() when a service method returns null for a requested resource. Return BadRequest() for business rule violations. Do not catch unhandled exceptions in actions; let global exception handling middleware produce the response.
- Apply [ProducesResponseType] to every action for its success status code and return type, and add [ProducesResponseType(StatusCodes.Status400BadRequest)] and [ProducesResponseType(StatusCodes.Status404NotFound)] when the action can return those results.
- Declare validation rules on request DTOs using data annotations. Rely on [ApiController] for automatic 400 Bad Request responses on invalid models; do not add manual ModelState.IsValid checks in actions.
- Inject services through the constructor, not directly in action methods.