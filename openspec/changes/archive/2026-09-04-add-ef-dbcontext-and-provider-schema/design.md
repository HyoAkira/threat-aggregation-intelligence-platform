## Context

The repository is a greenfield .NET 10 Clean Architecture solution with four
layers. `ThreatIntelligence.Infrastructure` has EF Core NuGet packages
(EFCore 10.0.11, SqlServer, Design, Tools) already installed in the csproj but
no `DbContext`, no entities, and no connection string wired. The API's
`appsettings.json` has no `ConnectionStrings` section. An MSSQL Server 2025
container is running locally with database `ThreatIntel` (SA user, port 1433,
password `REDACTED`).

The existing planning change `epic-1-skeleton-data-model` (full design in
`openspec/changes/epic-1-skeleton-data-model/design.md`) proposes a broader
canonical IOC model with a different connection string name (`ThreatIntel`)
and a JSON-metadata approach on `IocSource`. This change supersedes that
plan with a smaller, focused step: establish the EF connection with
`DefaultConnection`, a `Provider` entity, and provider-specific raw-storage
tables under a `providers` schema. No canonical `Ioc`/`IocSource` model is
built here.

## Goals / Non-Goals

**Goals:**
- `ThreatIntelDbContext` wired to `ConnectionStrings:DefaultConnection` (SQL
  Server provider).
- Migrations applied manually via `dotnet ef database update` (no auto-migrate at startup).
- `Provider` entity in `threat` schema; raw data tables `providers.ThreatFox`
  and `providers.PhishTank` with typed columns + unique constraints.
- Reproducible `docker-compose.yml` (mssql 2025 + api).
- API keys in user-secrets only.

**Non-Goals:**
- Provider fetcher logic, scheduled fetching, normalization rules.
- Canonical `Ioc` / `IocSource` / `FetchHistory` / `LogEntry` entities
  (deferred to a later change).
- Search API, React frontend.
- Production secret manager (Vault/CSI); CI variable wiring.

## Decisions

### 1. Connection string name: `DefaultConnection`
**Choice:** The connection string is stored at
`ConnectionStrings:DefaultConnection` in `appsettings.json`.
- Rationale: the user explicitly requested `DefaultConnection`. The existing
  epic-1 plan used `ConnectionStrings:ThreatIntel`; this change overrides it.
- The `ThreatIntel` database name is embedded in the connection string
  (`Initial Catalog=ThreatIntel`).

### 2. Provider table in `threat` schema, raw data in `providers` schema
**Choice:** The `Provider` configuration table lives in the `threat` schema.
Raw provider response tables (`ThreatFox`, `PhishTank`) live in a separate
`providers` schema.
- Rationale: `threat` schema groups the application's own configuration tables
  (future `Ioc`, `IocSource` will join here), while `providers` isolates raw
  source data. EF `ToTable("Name", "schema")` maps the correct schema.

### 3. API keys in user-secrets, not the database
**Choice:** `AuthKey__AbuseCh` and `PhishTank__ApiKey` are stored in
`dotnet user-secrets` (local) / `.env` (Docker), read at runtime via
`IConfiguration`. They never appear in the DB or in committed files.
- Rationale: security best practice; secrets must not live in the database
  or source control. The `Provider` entity stores only `Name`, `BaseUrl`,
  `IsEnabled`, `FetchIntervalMinutes`.

### 4. PhishTank `Verified` and `Online` stored as `bit`
**Choice:** The PhishTank API returns `'yes'/'no'` strings for `verified`
and `online`. These are converted to `bit` columns at the model level.
- Rationale: simpler querying and indexing than string columns. Conversion
  happens at fetch time (future change), not in this schema step.

### 5. PhishTank `Details` array stored as JSON
**Choice:** The `details` field (array of IP/cidr objects) is stored in an
`nvarchar(max)` column as JSON.
- Rationale: the array is variable-length and nested. A separate table would
  add complexity for an MVP; JSON is queryable via `JSON_VALUE` if needed
  later.

### 6. ThreatFox `Tags` stored as comma-separated string
**Choice:** The `tags` field (e.g. `"compromised,EtherHide"`) is stored as a
plain `nvarchar(max)` string.
- Rationale: preserves the raw format; parsing into a normalized table is
  YAGNI for now.

### 7. Docker: mssql 2025 + api service
**Choice:** `docker-compose.yml` with `sqlserver` (mcr.microsoft.com/mssql/
server:2025-latest) and `api` (builds from `ThreatIntelligence.API/`).
- `SA_PASSWORD` and API keys sourced from `.env` (gitignored).
- The API connection string points at `sqlserver:1433` (the Docker service
  name) so it works inside the container.

### 8. Migration strategy: manual `ef database update`
**Choice:** EF migrations are committed to the repository but applied
manually via `dotnet ef database update`, not auto-applied at startup.
- Rationale: the user prefers explicit migration control over auto-provisioning.

## Risks / Trade-offs

- **[Raw provider tables may grow large]** → Mitigation: no canonical model
  yet means no dedup across providers. When normalization lands, we can
  archive or purge raw tables after ingestion. Not blocking for MVP.
- **[MSSQL container is resource-heavy]** → Mitigation: pin `2025-latest`,
  document 4 GB+ RAM minimum. Follow-up spike may switch local dev to SQLite
  if team machines struggle.
- **[Unique constraints may reject partial re-fetches]** → Mitigation: use
  `Merge` or `INSERT ... ON CONFLICT` (upsert) pattern at the fetcher layer
  (future change) so re-fetches update rather than error.

## Migration Plan

1. Write the `ThreatIntelDbContext` + entity types +
   `EntityTypeConfiguration` classes.
2. Run `dotnet ef migrations add Initial --project ThreatIntelligence.API`
   to generate the migration script. Commit it.
3. Add `ConnectionStrings:DefaultConnection` to `appsettings.json`.
4. Create `docker-compose.yml` + `.env.template` + `.gitignore` entries.
5. Run `dotnet ef database update` to provision tables.
6. Verify: tables exist in `ThreatIntel` DB (`threat.Provider`,
   `providers.ThreatFox`, `providers.PhishTank`), no seed data required yet.

Rollback: `docker compose down -v` (drops the SQL volume; DB is disposable at
this stage).

## Open Questions

*(none — all design decisions were resolved with the user during planning)*
