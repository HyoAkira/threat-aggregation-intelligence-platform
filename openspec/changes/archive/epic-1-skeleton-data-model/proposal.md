## Why

The repository is still at the default .NET Clean Architecture template: four
layers, but only `Class1.cs` placeholders, no domain model, no persistence, and
no secrets strategy. Building the threat-source fetchers (Epic 2) now would
mean writing them against throw-away contracts. Epic 1 sets down the
irreducible foundation — a real data model, wired layers, EF Core + SQL Server,
one end-to-end MediatR query, and the abuse.ch / PhishTank secrets pipeline —
so that every subsequent epic plugs into stable, tested contracts instead of
re-deriving them.

## What Changes

- **Domain**: add `Ioc`, `IocSource`, `Provider`, `FetchHistory`, `LogEntry`
  entities plus the `IocType` enum. `Ioc` is the canonical, globally-deduplicated
  record and is uniquely keyed on `(IocType, NormalizedValue)`.
- **Application**: register **MediatR** + **FluentValidation** in DI; add one
  end-to-end smoke query (`PingQuery`) so Application→Infrastructure→Domain
  routing is provably wired before any real logic lands.
- **Infrastructure**: add `ThreatIntelDbContext` with Fluent
  `EntityTypeConfiguration` per entity; register EF + SqlServer in
  `AddInfrastructure` (first migration = `Initial`, creates 5 tables).
- **Persistence hosting**: `docker-compose.yml` running `mssql` + `api`; the API
  connection string targets the container host so `docker compose up` is the
  single local entry point.
- **Secrets**: `dotnet user-secrets` for local dev storing `AuthKey__AbuseCh`
  and `PhishTank__ApiKey`; `.env` + gitignored `appsettings.Development.json`
  for docker. No secret material is ever committed.
- **Seed data**: `Provider` rows (ThreatFox, PhishTank, URLhaus) seeded via EF
  `HasData` migration, not hard-coded in service code.

## Capabilities

### New Capabilities

- `data-model`: the canonical IOC data model — a deduplicated `Ioc` keyed on
  `(IocType, NormalizedValue)`, its per-provider `IocSource` sightings
  (first/last seen, raw value, provider-specific metadata), the `Provider`
  source registry, plus `FetchHistory` (per-run audit) and `LogEntry`
  (application log table for the Epic 3 log endpoint). This spec defines the
  entities, their fields, relationships, and the uniqueness/dedup contract.

### Modified Capabilities

*(none — no existing spec paths to delta)*

## Impact

- New `ThreatIntelligence` database schema via the first EF migration.
- New NuGet dependencies: `MediatR`, `FluentValidation`,
  `FluentValidation.DependencyInjectionExtensions`,
  `Microsoft.EntityFrameworkCore.SqlServer`, and tooling pack for migrations.
- `docker-compose.yml` becomes the local run command (`docker compose up`).
- The `PingQuery` endpoint exposes no threat data yet; it is a contract
  smoke-test. **No breaking API changes**, no existing consumer affected (the
  project has no consumers today).
- Secrets additions touch `.gitignore` only; production secret injection
  (Vault / CI variables) is deferred out of this change.
