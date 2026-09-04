## Why

The repository has EF Core NuGet packages installed in `ThreatIntelligence.Infrastructure`
but no `DbContext`, no connection string, and no entity models. The MSSQL Server
container is already running with a `ThreatIntel` database. Before any fetcher
logic can be written, we need the persistence layer wired up: a `DefaultConnection`
string, a `ThreatIntelDbContext`, and typed tables under a `providers` schema to
store raw API responses from each threat intelligence source (PhishTank, ThreatFox).

This is a small, focused step — no canonical `Ioc`/`IocSource` model yet. We
store raw provider data now; normalization and dedup come later.

## What Changes

- **Infrastructure**: Add `ThreatIntelDbContext` configured with
  `ConnectionStrings:DefaultConnection` (SQL Server provider); wire it in
  `AddInfrastructure` via `AddDbContext`. Migrations are applied manually via
  `dotnet ef database update`.
- **Domain**: Add `Provider` entity (`ProviderId`, `Name`, `BaseUrl`,
  `IsEnabled`, `FetchIntervalMinutes`). API keys are **not** stored here — they
  live in `dotnet user-secrets` and are read at runtime.
- **Infrastructure**: `EntityTypeConfiguration` for `Provider` plus two typed
  tables under the `providers` schema:
  - `providers.ThreatFox` — columns for every field in the ThreatFox API
    response (`IocValue`, `IocType`, `ThreatType`, `Malware`, `MalwareAlias`,
    `MalwarePrintable`, `FirstSeenUtc`, `LastSeenUtc`, `ConfidenceLevel`,
    `IsCompromised`, `Reference`, `Tags`, `Anonymous`, `Reporter`, `IocId`).
    Unique constraint on `(IocId, IocValue, IocType)` to prevent duplicate
    inserts on re-fetch.
  - `providers.PhishTank` — columns for the PhishTank API response (`PhishId`,
    `Url`, `PhishDetailUrl`, `SubmissionTime`, `Verified`, `VerificationTime`,
    `Online`, `Target`, `Details` stored as JSON for the nested details array).
    Unique constraint on `PhishId` to prevent duplicate inserts.
- **API**: Add `ConnectionStrings:DefaultConnection` to `appsettings.json`;
  migrations applied manually via `dotnet ef database update`.
- **Docker**: Add `docker-compose.yml` with `sqlserver`   (mcr.microsoft.com/mssql/server:2025-latest) + `api`
  services; `.env` for secrets; `.env` and `appsettings.Development.json` in
  `.gitignore`.
- **Secrets**: API keys (`AuthKey__AbuseCh`, `PhishTank__ApiKey`) stored via
  `dotnet user-secrets` (local) / `.env` (Docker); never committed.

## Capabilities

### New Capabilities

- `persistence/ef-core-dbcontext`: `ThreatIntelDbContext` registered with
  SQL Server provider, `DefaultConnection` string, manual EF migration workflow
  via `dotnet ef database update`.
- `persistence/provider-schema`: `Provider` entity and `providers` database
  schema with typed `ThreatFox` and `PhishTank` raw-response tables, including
  unique constraints for re-fetch idempotency.

### Modified Capabilities

*(none — root `openspec/specs/` is empty; no existing requirement paths to delta)*

## Impact

- **New files**: `ThreatIntelDbContext`, `Provider` entity,
  `EntityTypeConfiguration` classes, `docker-compose.yml`, `.env.template`.
- **Modified files**: `appsettings.json` (add `ConnectionStrings:DefaultConnection`),
  `DependencyInjection.cs` (register `ThreatIntelDbContext` via `AddDbContext`), `.gitignore` (`.env`),
  `ThreatIntelligence.Infrastructure.csproj` (already has EF Core packages —
  no new NuGet dependencies needed).
- **No breaking API changes** — the project has no consumers yet.
- **Database**: creates `ThreatIntel` database with `threat` and `providers` schemas, three tables
  (`threat.Provider`, `providers.ThreatFox`, `providers.PhishTank`), and unique indexes. Disposable in dev
  (`docker compose down -v`).

---

*Version: v1.0.0*
