---
description: 'Generate API documentation for the active controller file'
agent: 'copilot'
tools: ['search', 'read']
---
# Generate API Documentation

Analyze the controller in ${file} and generate comprehensive API documentation.

If ${file} contains more than one controller class, document the endpoints of each class under its own level-2 heading, using the class name.

If ${file} is not an API controller or contains no HTTP endpoints, do not invent endpoints. Respond only with: "The active file does not define any API endpoints."

For each endpoint in the controller:
1. List the HTTP method and route.
2. Describe the purpose of the endpoint.
3. Document the request parameters, body, and query string inputs.
4. Document the possible response status codes and their meanings.
5. Provide one example request and one example response for each endpoint. Show the response body in JSON format when the endpoint returns JSON. If the endpoint returns no body (for example, 204 No Content) or a non-JSON body, state the Content-Type when applicable and show "(no body)" or a short plain-text example instead.

Format the output as a Markdown document suitable for a developer wiki.