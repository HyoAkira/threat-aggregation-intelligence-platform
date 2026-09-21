## MODIFIED Requirements

### Requirement: Authorization policies

The system SHALL require authentication for every business endpoint except `POST /api/auth/login`, the public IOC query endpoints (`GET /api/iocs`, `GET /api/iocs/providers`, `GET /api/iocs/search`, `GET /api/iocs/{id}`, and `GET /api/iocs/{id}/relations`), the existing IOC AI report endpoints (`POST /api/iocs/{id}/ai/reports` and `POST /api/iocs/{id}/ai/reports/pdf`), the read-only MCP endpoint at `/mcp`, and `POST /api/iocs/{id}/chat`, which SHALL be usable without login. The system SHALL require the Admin role for all `/api/admin/*` endpoints, and SHALL allow anonymous access to the operational `/health` and `/health/live` endpoints used by container orchestration. Anonymous access to MCP or chat SHALL not grant access to any other protected endpoint.

#### Scenario: Anonymous IOC query allowed

- **WHEN** an unauthenticated request is made to a public IOC query endpoint under `/api/iocs`
- **THEN** the request is served without requiring a token

#### Scenario: Anonymous MCP tool call allowed

- **WHEN** an unauthenticated client initializes `/mcp` and calls an allowed read-only IOC tool
- **THEN** authorization does not require a token

#### Scenario: Anonymous chat allowed

- **WHEN** an unauthenticated visitor submits an IOC chat message
- **THEN** authorization does not require a token

#### Scenario: Anonymous request rejected

- **WHEN** an unauthenticated request is made to any other protected business endpoint
- **THEN** the request is rejected with 401

#### Scenario: Operational health probe allowed

- **WHEN** an unauthenticated request is made to `/health` or `/health/live`
- **THEN** authorization does not block the health response

#### Scenario: User denied admin endpoint

- **WHEN** an authenticated User requests an `/api/admin/*` endpoint
- **THEN** the request is rejected with 403

#### Scenario: Admin allowed admin endpoint

- **WHEN** an authenticated Admin requests an `/api/admin/*` endpoint
- **THEN** the request is allowed

