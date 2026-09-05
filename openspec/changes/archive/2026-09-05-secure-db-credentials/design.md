## Context

See proposal.md - Why. `appsettings.json` hardcodes the SA password; `docker-compose.yml` already interpolates `${SA_PASSWORD}`. The existing `.env` integration (spec: dotnet-env-integration) and `GetApiKey()` in `ThreatFoxApiService` already read secrets from `Environment.GetEnvironmentVariable` — this change applies the same established pattern to the DB connection string.

## Goals / Non-Goals

**Goals:**
- Zero secrets in committed configuration
- Password resolves from `SA_PASSWORD` environment variable at DbContext registration
- Connection string structure (server/db/user/options) still comes from `DefaultConnection` config
- Testable connection-string resolution logic

**Non-Goals:**
- No change to docker-compose (it already interpolates `${SA_PASSWORD}` correctly)
- No change to secrets storage format (still plain-text `.env`, gitignored)
- No rotation/encryption of existing `.env` contents

## Decisions

### Decision 1: Placeholder token in appsettings + interpolation in AddInfrastructure

`appsettings.json` keeps the full connection string with `Password=__DB_PASSWORD__` in place of the real secret. A small static helper, `DatabaseConnectionStringResolver.Resolve(string? configured, string? password)`, replaces the token with the `SA_PASSWORD` value (empty when unset). `AddInfrastructure` calls it with `configuration.GetConnectionString("DefaultConnection")` and `Environment.GetEnvironmentVariable("SA_PASSWORD")`.

Rationale: mirrors the existing `GetApiKey()` env-read pattern, keeps the config shape (server/db/user/options) declaration-first, and puts the password substitution in a tiny testable pure function. Alternatives considered: building the whole connection string from components in code (rejected — duplicates config and diverges from `DefaultConnection` convention); relying purely on docker-compose (rejected — local `dotnet run` would use the committed password).

### Decision 2: Resolver is a pure static helper in `ThreatIntelligence.Infrastructure`

`ThreatIntelligence.Infrastructure/ConnectionStrings/DatabaseConnectionStringResolver.cs` exposes a single static method. It is directly unit-testable without a WebApplicationFactory or SqlClient — no I/O, no DI.

### Decision 3: No change needed in docker-compose

The compose file already sets `ConnectionStrings__DefaultConnection` using `${SA_PASSWORD}` and `MSSQL_SA_PASSWORD`. Verification task confirms it, but no edits.

## Risks / Trade-offs

- [Committed password remains in git history] → Not rewritable in this scope; the secret should be rotated (change `SA_PASSWORD` in `.env` and DB). Documented here and in the acceptance note.
- [Empty password when SA_PASSWORD unset produces opaque auth error] → Intended per spec ("fail fast"); the startup health check (existing FailFast path) surfaces it.
- [Token string collision] → `__DB_PASSWORD__` is distinctive; unlikely to appear in user-input connection strings.

## Migration Plan

No data migration. Deploy: change `appsettings.json`, add resolver, wire in `AddInfrastructure`, rebuild, run tests. Existing docker environments need no change; local devs must have `SA_PASSWORD` in `.env` (already required by compose).

## Open Questions

None.