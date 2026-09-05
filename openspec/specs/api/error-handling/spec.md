# api/error-handling Specification

## Purpose
Establishes a single, uniform error-handling contract for the API. Exceptions raised anywhere in the request pipeline are converted to RFC-7807 `ProblemDetails` responses with a consistent status-code mapping, eliminating per-controller try/catch blocks.

## Requirements

### Requirement: Exceptions are converted to ProblemDetails responses

The system SHALL convert any exception that propagates to the HTTP layer into an RFC-7807 `ProblemDetails` response with `application/problem+json` content type.

#### Scenario: Unhandled exception produces a ProblemDetails response
- **WHEN** an exception propagates out of an action handler
- **THEN** the response has HTTP content type `application/problem+json`
- **AND** the response body is a ProblemDetails document with a `title` and `status` field
- **AND** the status matches the exception mapping

### Requirement: Known exceptions map to specific status codes

The system SHALL map exceptions to status codes as follows:
- `InvalidOperationException` whose message contains `not configured` → 401 Unauthorized
- `HttpRequestException` → 502 Bad Gateway
- `InvalidDataException` → 422 Unprocessable Entity
- any other exception → 500 Internal Server Error

#### Scenario: Missing configuration returns 401
- **WHEN** a handler throws an `InvalidOperationException` with message containing `not configured`
- **THEN** the response status is 401 Unauthorized
- **AND** the ProblemDetails message describes the missing configuration

#### Scenario: Upstream HTTP failure returns 502
- **WHEN** an API service throws an `HttpRequestException`
- **THEN** the response status is 502 Bad Gateway

#### Scenario: Malformed upstream data returns 422
- **WHEN** an API service throws an `InvalidDataException`
- **THEN** the response status is 422 Unprocessable Entity

#### Scenario: Unknown exception returns 500
- **WHEN** any other exception propagates
- **THEN** the response status is 500 Internal Server Error
- **AND** the response body does NOT leak internal exception details (generic message only)

### Requirement: Controllers do not handle exceptions

Controllers SHALL NOT contain try/catch blocks. Exceptions propagate to the exception-handling pipeline. Success responses SHALL return typed DTOs directly.

#### Scenario: Action without try/catch
- **WHEN** a controller action dispatches a command via `IMediator`
- **THEN** the action has no try/catch wrapper
- **AND** errors are handled by the exception-handling pipeline

### Requirement: Handler exceptions propagate unwrapped

Exceptions raised inside MediatR handlers SHALL propagate through the pipeline unwrapped so the middleware can map them. The Application layer SHALL NOT swallow or wrap exceptions.

#### Scenario: Handler exception propagates to middleware
- **WHEN** a handler throws an exception
- **THEN** the exception propagates through MediatR unmodified
- **AND** reaches the exception-handling middleware

### Requirement: ProblemDetails schema is visible in OpenAPI

The system SHALL register ProblemDetails support such that OpenAPI documents reflect `ProblemDetails` error responses for endpoints.

#### Scenario: OpenAPI shows ProblemDetails schema
- **WHEN** the OpenAPI document is generated
- **THEN** error responses for endpoints include a `ProblemDetails` schema
