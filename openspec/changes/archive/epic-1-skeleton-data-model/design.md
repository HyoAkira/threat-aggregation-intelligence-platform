## Context

(see `proposal.md` - Why.) The repository is a greenfield .NET 10 Clean
Architecture solution with four empty-ish layers and a placeholder DB-less
`Program.cs`. There is no existing database, no real domain, and no consumer
traffic, so this design optimizes for a **stable, minimal foundation** that
Epics 2–5 can extend without schema churn. The full requirement text lives in
`specs/data-model/spec.md`; this doc covers only the *how*.

Layer dependency direction: **Domain ← Application ← Infrastructure ← API**.
Domain must have zero framework references; EF Core lives in Infrastructure and
is hidden from Application behind an interface.

## Goals / Non-Goals

**Goals:**
- A real, migration-backed data model that satisfies `specs/data-model/spec.md`.
- EF Core + SQL Server reachable via `docker compose up`, with first-run DB
  provisioning automatic.
- A MediatR + FluentValidation skeleton, proven end-to-end by `PingQuery`.
- Secrets never committed; same code path works under `dotnet run` and Docker.

**Non-Goals:**
- Provider fetchers, normalization rules, search API, React frontend (Epics 2–4).
- Materialized `FirstSeen`/`LastSeen` on `Ioc` (computed on read for now).
- Production secret manager (Vault/CSI); CI variable wiring is out of scope.
- SQL Server HA, backups, or multi-region concerns.

## Decisions

### 1. Dedup key stored as a single normalized-value column + DB unique constraint
**Choice:** `Ioc.NormalizedValue` is a plain `string` column, and the unique key
is enforced at the **database** level via a unique index on
`(IocType, NormalizedValue)` — not in application code.
- Rationale: the dedup invariant must hold under concurrent fetches (Epic 3).
  App-level checks can race; the DB unique index is the source of truth. Insert
  conflicts (duplicate key) then drive the upsert path.
- Alternative considered: a hash column (e.g. MD5 of the value) for index size.
  Rejected — premature with a greenfield dataset; a string unique index is
  simpler and indexable. Revisit if table size warrants.
- **Boundary:** the *normalization algorithm itself* (lowercasing, punycode,
  IPv4 octet rules, URL canonicalization) is Epic 2.2. This change only
  reserves the column + constraint.

### 2. `IocType` stored as a string-backed enum
**Choice:** `IocType { IPv4, Domain, URL }` persisted as text (`"IPv4"` etc.)
via EF `HasConversion<string>()`.
- Rationale: human/grep-friendly in migrations and logs; resilient to enum
  reorder. `ValueObject`/`string` types would be over-engineering for an enum.

### 3. Provider-specific metadata is a JSON column on `IocSource`
**Choice:** a nullable `Metadata` column (`nvarchar(max)`) holds per-source,
provider-specific blobs (ThreatFox malware family, URLhaus labels, PhishTank
`verified`, Spamhaus expiry).
- Rationale: providers are heterogeneous; a typed schema would require a
  provider-specific column per source and is deferred until search needs typed
  filtering (a real, measurable need). JSON on SQL Server is queryable via
  `JSON_VALUE` when that day comes.
- Alternative considered: separate provider-specific tables. Rejected as
  YAGNI for a 3-source MVP.

### 4. No materialized `FirstSeen`/`LastSeen` on `Ioc` yet
**Choice:** `Ioc` exposes only `CreatedAt`/`UpdatedAt` (row lifecycle). The
global first/last-seen is derived by aggregating `IocSource.FirstSeen` /
`LastSeen` on read.
- Rationale: materializing invites a sync bug (a sighting that fails to update
  the global column corrupts search). The scheduled-fetch cadence (Epic 2.3) is
  low-frequency; an aggregate query is acceptable for MVP.
- When to revisit: if the Epic 3 search API shows aggregate-query latency
  problems, add a maintained column with a transaction that updates it in the
  same statement as the upsert.

### 5. Clean Architecture: Application owns the persistence abstraction
**Choice:** Application defines `IThreatIntelDbContext` (read surface:
`DbSet<Provider>`, `DbSet<Ioc>`) and Infrastructure provides the EF
`ThreatIntelDbContext` implementing it. `PingQuery` depends on
`IThreatIntelDbContext` only.
- Rationale: keeps Application free of EF/SQL dependency at compile time and
  keeps the smoke test genuine (it fails if DI wiring or the DB is broken).
- Alternative considered: DbContext-in-Application (faster to write). Rejected
  — violates the dependency rule for this template; the cost is one trivial
  interface.

### 6. Provider seed via EF `HasData` with stable GUIDs
**Choice:** `Provider` seed rows use **fixed, hard-coded GUIDs** inside
`ProviderConfiguration.HasData`, so the same identifiers resolve in every
environment (dev + docker + future staging).
- Rationale: avoids re-seeding or FK churn across migrations; the Provider is
  the anchor row referenced by `IocSource`.

### 7. Secrets: `dotnet user-secrets` for dev, `.env` for Docker
**Choice:** local `dotnet run` reads `AuthKey__AbuseCh` / `PhishTank__ApiKey` /
`ConnectionStrings:ThreatIntel` from `appsettings.Development.json` +
user-secrets. Docker-compose injects the same keys from a gitignored `.env`
into the `api` container's environment.
- Rationale: one config shape, two backing stores, nothing committed.
- Note: `.gitignore` already covers `appsettings.Development.json`; a `.env`
  entry is added here.

### 8. First-run DB provisioning via migration-on-start
**Choice:** the API startup calls `dbContext.Database.Migrate()` (idempotent)
before serving requests, so `docker compose up` on a fresh machine yields a
ready database without a manual `ef database update` step.
- Rationale: greenfield, no migration-order surprises. The `Initial` migration
  is the only one in this change.
- Rollback: drop the container volume (`docker compose down -v`); the DB is
  disposable and contains only seed data at this stage.

## Risks / Trade-offs

- **[SQL Server container is resource-heavy]** → Mitigation: pin
  `mssql/server:2022-latest` with documented 4 GB+ RAM minimum; record a
  follow-up spike to gate local dev behind SQLite (switchable provider) if
  team machines struggle. Not blocking Epic 1.
- **[Upfront schema is costly to change later]** → Mitigation: keep `Ioc`
  narrow (no optional columns yet); typed metadata deferred per Decision 3.
- **[`HasData` GUID coupling to migration IDs]** → Mitigation: only stable,
  hard-coded GUIDs are used; no random GUIDs in seed data, so re-running
  migrations never creates duplicate providers.
- **[Concurrent upserts at first fetch (Epic 2) may hit the unique key]** →
  Mitigation: reserved here via the DB unique index (Decision 1) so the conflict
  path is deterministic (`IocSource` insert, `Ioc` reuse) — no app-level lock
  needed.

## Migration Plan

Greenfield — no live data:
1. Author `Initial` migration (`dotnet ef migrations add Initial` from the
   startup project); it creates the 5 tables, the `(IocType, NormalizedValue)`
   unique index, and seeds the 3 providers.
2. Commit the migration file only (no data).
3. `docker compose up` starts `sqlserver` + `api`; the API's
   `Database.Migrate()` auto-applies `Initial` on first boot.
4. Verify via `PingQuery` response and direct table inspection.

Rollback: `docker compose down -v` (drops the SQL volume; no durable data
exists at this stage).

## Open Questions

- **SQLite for local dev vs. SQL Server everywhere.** The plan mandates SQL
  Server + `docker-compose.yml`. SQL Server's container footprint is heavy; a
  later spike may switch local dev to SQLite with the same `DbContext` (EF
  abstracts the provider). Resolved in a future change, not this one.
- **Materialized global first/last-seen.** Per Decision 4, deferred; revisit
  only if the Epic 3 search API exhibits read-latency regressions on the
  aggregate.
