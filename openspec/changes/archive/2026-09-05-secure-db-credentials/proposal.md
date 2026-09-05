## Why

`appsettings.json` contains a hardcoded SQL Server password (`ver1db@rotten`) committed to source control. `docker-compose.yml` already uses `${SA_PASSWORD}`, but local `dotnet run` still reads the committed secret. The password must come from the environment, never from source.

## What Changes

- Replace the hardcoded password in `appsettings.json` with a placeholder token
- AddInfrastructure resolves `SA_PASSWORD` from the environment (matching the existing `GetApiKey()` pattern) and interpolates it into the connection string
- `docker-compose.yml` already interpolates `${SA_PASSWORD}` — no change needed there (verify only)
- Add unit tests for the connection-string resolution logic

## Capabilities

### New Capabilities

- `persistence/database-credentials`: Defines how the DB connection string is resolved so secrets never live in source control.

### Modified Capabilities

- `persistence/ef-core-dbcontext`: DefaultConnection password is sourced from the `SA_PASSWORD` environment variable instead of committed configuration.

## Impact

- `ThreatIntelligence.API/appsettings.json` — placeholder connection string
- `ThreatIntelligence.Infrastructure/DependencyInjection.cs` — env-based password resolution
- `ThreatIntelligence.Infrastructure/` — small resolvable helper for testability
- `tests/ThreatIntelligence.Application.Tests` — new tests
- `docker-compose.yml` — verified unchanged (already env-driven)