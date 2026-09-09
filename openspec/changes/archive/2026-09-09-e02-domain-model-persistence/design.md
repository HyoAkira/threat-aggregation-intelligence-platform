## Context

See proposal.md — Why. The domain POCOs and byte-backed enums already exist from E01 but are unconstrained: no DbContext, no mappings, no constraints/indexes, no migration, no seeds, no repository/unit-of-work, and no hashing. The infrastructure project already references EF Core 10.0.12 + `Microsoft.EntityFrameworkCore.SqlServer`, and `docker-compose.yml` provides `mssql` (1433) plus the `testsqlserver` profile (1432). Both API and Worker hosts currently call `AddInfrastructureServices`. The schema target is Technical Specification §4; identity semantics are FR-004/FR-002.

## Goals / Non-Goals

**Goals:**
- A single shared DbContext that materializes the §4 schema exactly (naming, types, nullability, constraints, indexes).
- A deterministic initial migration that applies cleanly to an empty database and is idempotent thereafter.
- Idempotent provider seeds and a first-admin seed workflow with deterministic SQL-backed outcomes, including concurrent execution.
- Foundational repository + unit-of-work abstractions and a SHA-256 hashing service used by downstream ingestion (E08) and search (E09).
- Strict project dependency direction: Domain ← Application ← Infrastructure ← hosts.

**Non-Goals:**
- No normalization/validation logic (E03), auth/password KDF (E04), messaging/fetch lifecycle (E05), or the upsert/dedup pipeline (E08).
- No formal migration-job ownership policy — both hosts migrate idempotently now; the explicit migration job is deferred to E14 (TI-142).
- No changes to the public API or the `FetchJob` message contract.

## Decisions

**D1. Nullable-port uniqueness via a computed identity column.** SQL Server's default unique index treats NULLs as distinct, which would silently allow duplicate bare (NULL-port) rows for the same provider/type/hash — contradicting FR-002 (only *different ports* produce separate source entries). The `port` CHECK (NULL or 1–65535) makes `0` a safe sentinel, so we persist a stored computed column `portKey = COALESCE([port], 0)` (mapped as a shadow property so the domain entity stays clean) and build the unique index on `(providerId, iocType, iocValueHash, portKey)`.
- *Alternatives considered:* (a) plain unique index on `(providerId, iocType, iocValueHash, port)` — rejected: NULLs distinct, allows duplicate bare rows; (b) filtered unique index excluding NULL ports — rejected: removes uniqueness on the bare case entirely; (c) sentinel `-1` in a mapped property — rejected: `0` is provably out of the CHECK range and needs no domain property.

**D2. One shared `ThreatIntelligenceDbContext` in Infrastructure, configured per-entity.** One `IEntityTypeConfiguration<T>` class per aggregate (`ProviderConfiguration`, `IoCConfiguration`, `TagConfiguration`, `IoCTagConfiguration`, `FetchHistoryConfiguration`, `UserConfiguration`, `SettingsConfiguration`) under `Infrastructure/Persistence/Configurations`. Mappings set plural lower-camel table names, explicit `nvarchar` lengths, nullability, `binary(32)` fixed length for `iocValueHash`, `datetime2` timestamps, `tinyint` enum ordinals, relationships, and the index/constraint set from §4.
- *Alternative considered:* a single `OnModelCreating` blob — rejected for reviewability and "strict boundaries" tasking.

**D3. Hashing as an Application abstraction, BCL implementation.** `IValueHasher` lives in `Application/Abstractions` (search and ingestion both need it), implemented in Infrastructure with `System.Security.Cryptography.SHA256` over the UTF-8 bytes of the normalized value, returning exactly 32 bytes. A `ValueEquality` helper (constant-time-ish compare plus plain string compare after hash lookup) satisfies the collision-detection scenario.
- *Alternative considered:* implement in Domain — rejected: keeps Domain free of persistence-identity concerns; hashing is a storage identity concern.

**D4. Startup migration + provider-seed bootstrap shared by both hosts.** `DatabaseInitializer` in Infrastructure exposes `InitializeAsync(CancellationToken)` that runs `context.Database.MigrateAsync()` then seeds providers. Both API and Worker invoke it during startup (API after build; Worker in `Worker.StartAsync`). Idempotency is enforced by existence checks plus DB unique indexes, so overlapping startup between the two hosts is safe. E04 extends the host bootstrap to invoke the first-admin seed after registering the production password hasher.
- *Alternative considered:* `Database.EnsureCreatedAsync()` — rejected: no migration history, breaks the E14 upgrade story.

**D5. Provider seeding is insert-if-absent only.** Seed ThreatFox (id 1, `https://threatfox-api.abuse.ch`, enabled, 5 min) and PhishTank (id 2, `https://data.phishtank.com`, enabled, 8 h) with explicit IDs and `nextFetchTime = NULL`, but only when the row is missing. Existence check is by id, so operator changes to enabled/interval are never overwritten.

**D6. Admin seeding depends on an authentication-owned password-hasher abstraction.** E02 implements the persistence workflow against `IPasswordHasher` and verifies it with a deterministic test implementation; E04 provides the production PBKDF2/BCrypt implementation and startup activation. When no Admin exists, the workflow hashes `ADMIN__PASSWORD` immediately before insert and never logs or persists plaintext. An existing non-Admin with `ADMIN__USERNAME` is a configuration conflict: startup fails clearly and the existing user is not modified. Concurrent attempts are arbitrated by the unique `users.username` index; after a unique violation the loser re-reads state and succeeds only if the winner created the configured Admin, otherwise it reports a conflict.
- *Alternatives considered:* (a) implement a password KDF in E02 — rejected because E04 owns authentication cryptography; (b) treat any same-name row as success — rejected because it can leave a deployment with no Admin; (c) promote or reset the existing user — rejected as destructive.

**D7. Repository + unit-of-work abstractions with history outside the IOC transaction.** `IIocRepository` resolves candidates by `(providerId, iocType, iocValueHash, portKey)` and by `(providerId, providerRecordId)`. Every hash-based result is confirmed against the stored normalized value before it is treated as equal. A minimal grouped read returns rows for one exact `(iocType, normalizedValue)` identity without defining E09 pagination or response DTOs. `IFetchHistoryRepository` persists and updates history independently. A small `IUnitOfWork` wraps `BeginTransactionAsync`/`CommitAsync`/`RollbackAsync`; the E08 pipeline later owns chunking and atomic upsert orchestration.

**D8. Fixed UTC timestamps and injectable time.** Domain writes set `CreatedAt`/`UpdatedAt`/history timestamps from `TimeProvider` (registered as singleton `TimeProvider.System`, replaceable in tests). No local-time conversion anywhere in persistence.

## Risks / Trade-offs

- [Computed column `portKey` couples schema to the CHECK range] → The sentinel `0` is impossible for a real port because the CHECK constraint is enforced on the same column; a unit + SQL-backed test asserts the constraint pair together.
- [Both hosts migrating at startup can race] → `MigrateAsync` and provider seeds are idempotent and index-guarded. Formal single-owner migration execution moves to E14 (TI-142).
- [`binary(32)` hash index is only useful for equality] → That is all identity requires (exact match, upsert targeting); value comparison after hash lookup covers collision detection.
- [Shadow computed property reduces model discoverability] → Documented here and in the configuration class; it stays out of the Domain entity so domain invariants remain the single source of truth for state.

## Migration Plan

- The initial `InitialCreate` migration is generated from the finalized configurations and applied at startup by both hosts (`DatabaseInitializer`). On an empty database it creates all tables, constraints, and indexes in one clean step; on an existing database it is a no-op because migrations are tracked.
- Provider seeds are part of the E02 bootstrap. E04 adds the production password hasher and activates first-admin seeding without changing the E02 persistence rules.
- Rollback for dev/test is `dotnet ef database update` (no down migration needed pre-release) or re-creating the disposable test database; a formal forward/backward migration and rollback runbook belongs to E14 (TI-142/TI-143).

## Open Questions

None that would change the specs or task breakdown. The SQL-backed integration tests (TI-132/TI-138) will later prove the constraint set against a real SQL Server, but the schema itself is fully specified by §4.
