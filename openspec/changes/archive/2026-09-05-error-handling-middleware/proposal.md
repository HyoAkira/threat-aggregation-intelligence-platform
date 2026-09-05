## Why

Controllers currently duplicate error handling in every action (try/catch with 401/502/500 status mappings), producing inconsistent error shapes and leaking exception messages. There is no single place that converts exceptions into uniform, RFC-7807 error responses.

## What Changes

- Add a MediatR exception-handling pipeline behavior so Application-layer handlers propagate typed exceptions without per-handler try/catch
- Add global ProblemDetails middleware in the API layer that maps exceptions to RFC-7807 `ProblemDetails` responses
- Register the middleware and pipeline behavior in dependency injection
- Remove the per-controller try/catch blocks; actions return typed DTOs directly
- Preserve the existing status mapping: not-configured → 401, `HttpRequestException` → 502, malformed data → 422, fallback → 500 with a generic message
- Add unit tests for the middleware and pipeline behavior

## Capabilities

### New Capabilities

- `api/error-handling`: Defines the contract for converting exceptions into uniform RFC-7807 ProblemDetails responses, including the status-code mapping.

## Impact

- `ThreatIntelligence.API/Middleware/` — new ProblemDetails middleware
- `ThreatIntelligence.Application/Behaviors/` — new exception pipeline behavior
- `ThreatIntelligence.API/Program.cs` — middleware registration order
- `ThreatIntelligence.API/Controllers/*.cs` — remove try/catch
- `tests/ThreatIntelligence.Application.Tests` — new tests