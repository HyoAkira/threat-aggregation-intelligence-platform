## Purpose

Defines the API-wide error contract: every non-success response is a safe, consistent RFC 7807 ProblemDetails object that carries the request correlation id, so clients and operators can classify and trace failures uniformly.

## ADDED Requirements

### Requirement: Consistent RFC 7807 ProblemDetails responses
Every authenticated IOC query endpoint and the shared error handling SHALL map failures to RFC 7807 ProblemDetails with the appropriate HTTP status: invalid input and validation failures 400, unknown resources 404, unauthenticated requests 401, forbidden (non-Admin on an Admin route) 403, guarded conflicts 409, and unexpected errors 500. Each problem detail SHALL include a `type`, a stable machine-readable `code`, a human `title`, the HTTP `status`, a `detail` safe for end users, and the request's `correlationId`. Messages SHALL NOT leak secrets, credentials, connection strings, or full IOC payloads.

#### Scenario: Validation failure is a 400 problem detail
- **WHEN** a client sends invalid input
- **THEN** the response is 400 with a problem detail whose code identifies the validation class and whose detail names the offending input property

#### Scenario: Missing resource is a 404 problem detail
- **WHEN** a client references an unknown IOC id
- **THEN** the response is 404 with a not-found problem detail

#### Scenario: Unexpected error is a safe 500
- **WHEN** an unhandled exception occurs
- **THEN** the response is 500 with a generic problem detail that omits stack traces and internal state

### Requirement: Correlation id is threaded through errors
The API SHALL accept an `X-Correlation-ID` request header or generate one when absent, SHALL echo it on the response, SHALL include it in every ProblemDetails body, and SHALL log it with the corresponding structured log entry. Clients SHALL therefore be able to correlate a problem detail with server logs.

#### Scenario: Supplied correlation id appears on the error
- **WHEN** a client sends `X-Correlation-ID: abc-123` and the request fails
- **THEN** the problem detail and response header both carry `abc-123`

#### Scenario: Missing correlation id is generated and surfaced
- **WHEN** a client sends no correlation header and the request fails
- **THEN** a generated correlation id appears in the problem detail and response header and matches the logged request
