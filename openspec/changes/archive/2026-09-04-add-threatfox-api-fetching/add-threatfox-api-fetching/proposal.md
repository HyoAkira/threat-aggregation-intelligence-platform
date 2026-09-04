## Why

The platform needs to fetch IOCs from ThreatFox (abuse.ch) to build a threat intelligence aggregation system. Currently, the `providers.ThreatFox` table exists with typed columns, but no code exists to populate it. We have an API key (`AuthKey__AbuseCh`) already configured in `.env`. The first step is implementing fetching and persistence for ThreatFox IOCs.

## What Changes

- **New**: `ThreatFoxApiService` in `ThreatIntelligence.Infrastructure` — service that calls the ThreatFox Export API (`/v2/files/exports/{apiKey}/...`), fetches `full.json.zip`, decompresses the ZIP, parses JSON IOCs, and upserts into `providers.ThreatFox`
- **New**: `DownloadDatasetAsync()` method — fetches the full IOC dataset (all IOCs, generated hourly), decompresses ZIP, parses JSON, upserts records
- **New**: `RecentIoCsAsync()` method — fetches recent IOCs (last 48h, generated every 5 minutes), parses JSON, upserts records
- **New**: `ThreatFoxController.cs` in `ThreatIntelligence.API/Controllers/` with `GET /api/threatfox/download` and `GET /api/threatfox/recent` endpoints
- **New**: `LoadEnvFile()` helper function in `Program.cs` — loads `.env` into environment variables at startup for local development (no external packages; pure .NET). Docker-compose already maps `.env` to container env vars
- **Update**: `Infrastructure/DependencyInjection.cs` — register `ThreatFoxApiService` with `HttpClient` configured with base address `https://threatfox-api.abuse.ch`
- **Update**: `Program.cs` — add `LoadEnvFile()` call before `CreateBuilder()` to load `.env` into environment variables
- **Remove**: `DotNetEnv` NuGet package from `ThreatIntelligence.API.csproj` (not needed — `LoadEnvFile()` is a pure .NET helper)
- **Update**: `.env` file — set `AuthKey__AbuseCh=516c0984949e0c979b15c7841c3e42397e565128900124c0` (single source for both local development and docker-compose)
- **Update**: Clear conflicting user-secrets (`AuthKey__AbuseCh`, `PhishTank__ApiKey`) so `.env` takes precedence

### Decisions from Interview

| Aspect | Decision |
|---|---|
| Export format | `full.json.zip` (all IOCs, with ZIP decompression) |
| Duplicate handling | Upsert via SQL Server `MERGE` on `(IocId, IocValue, IocType)` |
| Error handling | Throw exception on failure (caller handles) |
| Auth key location | In URL path (`/v2/files/exports/{apiKey}/full.json.zip`) |
| Empty response | Throw exception on empty/NoContent |
| Invocation | Manual API endpoint (not background service yet) |
| Format | JSON (`.json` / `.json.zip`) |

## Capabilities

### New Capabilities
- `threatfox-api-fetching`: Fetching IOCs from ThreatFox Export API (full dataset and recent IOCs) and persisting to `providers.ThreatFox` table with upsert semantics

### Modified Capabilities
- `persistence/ef-core-dbcontext`: Adding ThreatFoxApiService registration alongside existing DbContext registration in `DependencyInjection.cs` — this is a new service registration, not a requirement change to the existing spec. May add `HttpClient` registration for the service.
- `persistence/provider-schema`: No requirement changes — the ThreatFox table schema already supports the IOC fields. This change implements the fetching/persistence that was specified but not yet implemented.

## Impact

- **`ThreatIntelligence.Infrastructure` project**: New `ThreatFoxApiService.cs` file, updated `DependencyInjection.cs`
- **`ThreatIntelligence.API` project**: New controller for manual fetch endpoints, `.env` updated with API key
- **Database**: No schema changes needed — `providers.ThreatFox` table already exists with all columns and unique constraint
- **Configuration**: `.env` file populated with ThreatFox API key; loaded as environment variables via custom `LoadEnvFile()` helper for local development and via docker-compose environment mapping for containerized deployment. User-secrets cleared to avoid conflicts.
- **Dependencies**: Uses `System.Net.Http` and `System.IO.Compression` (both .NET built-ins, no new NuGet packages)
- **No breaking changes** — adds new functionality without modifying existing code paths
