## Context

The project is a .NET 10.0 Clean Architecture solution with four projects (Domain, Application, Infrastructure, API). The `providers.ThreatFox` table already exists with typed columns and a unique constraint on `(IocId, IocValue, IocType)`. The `AuthKey__AbuseCh` configuration key is already present in `.env` but `REPLACE_WITH_YOUR_KEY`. No ThreatFox API fetching code exists — no HttpClient, no service, no controller. The project uses MediatR + CQRS (skeleton only — no handlers exist yet). The `DependencyInjection.cs` in Infrastructure currently only registers `ThreatIntelDbContext`. No Refit package is installed in the project; only EF Core packages are present.

## Goals / Non-Goals

**Goals:**
- Fetch IOCs from the ThreatFox Export API and persist to `providers.ThreatFox`
- Support both `full.json.zip` (full dataset) and `recent.json` (last 48h) exports
- Upsert on duplicate `(IocId, IocValue, IocType)` to avoid duplicates
- Expose manual HTTP endpoints for triggering fetches
- Use only .NET built-in libraries (no new NuGet packages)

**Non-Goals:**
- Background/timer-based scheduling (deferred to Epic 2.3)
- Implementing the `IThreatSourceProvider` interface pattern from `project.md` (not yet created)
- UI components or React frontend
- Normalized `Ioc` table (Epic 3.1) — storing raw ThreatFox rows only in this change

## Decisions

### 1. HTTP client approach: HttpClient, not Refit
**Chosen**: Use `HttpClient` directly with `IHttpClientFactory`.
**Alternatives considered**: Refit — the project plan (`project.md` Epic 2.2) mentions "Refit client per provider" for future phases, but no Refit package is installed yet. Installing a new dependency for a single integration is not justified when `HttpClient` is built-in and sufficient.
**Rationale**: Minimal dependencies, avoids adding a package that the broader refactoring toward `IThreatSourceProvider` would handle anyway. No external packages added.

### 2. Upsert strategy: SQL Server MERGE
**Chosen**: Use raw SQL `MERGE INTO` statement for upsert.
**Alternatives considered**: 
- EF Core `ExecuteUpdate`/`ExecuteInsert` (EF Core 7+) — requires loading all records into memory and comparing
- Check-then-insert/update pattern — prone to race conditions
**Rationale**: `MERGE` is a single atomic SQL statement, handles both insert and update in one pass. SQL Server natively supports it.

### 3. ZIP decompression: System.IO.Compression
**Chosen**: Use `ZipArchive` to decompress `full.json.zip`.
**Alternatives**: External libraries like SharpZipLib — unnecessary since `System.IO.Compression` handles ZIP natively in .NET.
**Rationale**: Built-in, no dependencies.

### 4. JSON parsing: System.Text.Json
**Chosen**: Use `JsonDocument` for schema-less parsing.
**Alternatives**: Strongly-typed model with `JsonSerializer.Deserialize<T>` — but ThreatFox returns different JSON shapes for different IOC types, making a single DTO impractical. Dynamic parsing is more flexible.
**Rationale**: The ThreatFox API returns objects with varying fields depending on IOC type (hash, URL, domain, IP). Using `JsonDocument` with `TryGetProperty` allows graceful handling of missing fields.

### 5. API key handling: .env as single source with user-secrets cleared

**Chosen**: Embed API key in the URL path as `/v2/files/exports/{key}/...`. The `.env` file is the single source of truth for both local development and docker-compose.

**Dual environment loading strategy:**
- **Local development**: `LoadEnvFile()` helper in `Program.cs` reads `.env` file and sets `Environment.SetEnvironmentVariable()` before `WebApplication.CreateBuilder(args)` is called. ASP.NET Core's default `AddEnvironmentVariables()` provider picks up these vars into `IConfiguration`
- **Docker-compose**: `environment: - AuthKey__AbuseCh=${AuthKey__AbuseCh}` in `docker-compose.yml` maps `.env` to container env vars
- **User-secrets**: Conflicting user-secrets (`AuthKey__AbuseCh`, `PhishTank__ApiKey`) are removed via `dotnet user-secrets remove` to prevent override of `.env` values
- **Config key format**: Uses `AuthKey:AbuseCh` (colon-separated) because .NET's `AddEnvironmentVariables()` converts `__` (double underscore) to `:` when storing env vars in configuration. Accessing `_configuration["AuthKey__AbuseCh"]` (double underscore) returns null; must use `_configuration["AuthKey:AbuseCh"]` (colon)

**Key insight from debugging**: In .NET 10, `IConfiguration["AuthKey__AbuseCh"]` (double underscore) does NOT match the config key `AuthKey:AbuseCh` (colon) that `AddEnvironmentVariables()` creates from the env var `AuthKey__AbuseCh`. The service must use the colon-separated key.

**Rationale**: This matches the ThreatFox API's documented pattern (see `ApiDocs/ThreatFox - Export.md` line 31: `curl -i "https://threatfox-api.abuse.ch/v2/files/exports/YOUR-AUTH-KEY-HERE/full.csv.zip"`). The approach ensures `.env` is the single source for both environments without requiring external packages.

### 6. Service location: Infrastructure layer
**Chosen**: Place `ThreatFoxApiService` in `ThreatIntelligence.Infrastructure`.
**Rationale**: It depends on `ThreatIntelDbContext` (Infrastructure), `IConfiguration` (Microsoft.Extensions), and `HttpClient` (Infrastructure-scoped). Keeping it in Infrastructure follows the Dependency Inversion principle — the API layer injects it.

### 7. Manual trigger: API controller endpoint
**Chosen**: Create a controller in `ThreatIntelligence.API` with `GET /api/threatfox/download` and `GET /api/threatfox/recent`.
**Rationale**: Current scope is manual invocation only (no background service). A controller endpoint provides the simplest trigger mechanism.

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| **Full dataset is large** (potentially millions of rows) | Process in batches; if memory pressure occurs, use streaming/`yield` pattern |
| **API rate limits** | Full dump is hourly, recent is every 5 min — current scope is manual triggering so frequency won't hit limits |
| **ZIP archive entry name mismatch** | Validate entry name `full.json` exists before parsing; throw descriptive error if not found |
| **SQL MERGE deadlock potential** | Unlikely at our throughput; if issues arise, switch to batch `ExecuteUpdate` |
| **JSON field mapping drift** | Use `TryGetProperty` with fallback chains (e.g., `value` → `md5` → `sha256`); log unmapped fields for visibility |

## Migration Plan

This is a greenfield implementation — no migration needed:
1. `.env` file is the single source of truth for API keys
2. `.env` is loaded into environment variables via a custom `LoadEnvFile()` helper in `Program.cs` (no external packages)
3. Conflicting user-secrets (`AuthKey__AbuseCh`, `PhishTank__ApiKey`) are removed to prevent override
4. `docker-compose.yml` already maps `AuthKey__AbuseCh=${AuthKey__AbuseCh}` for container deployment
5. `providers.ThreatFox` table already exists with all columns and unique constraint
6. No database schema changes required

## Open Questions

None — all decisions were resolved during the user interview (see proposal.md "Decisions from Interview").
