## Why

The data contract is specified (Technical Specification §4, PRD FR-004/FR-006) but the domain is only partially scaffolded: entity and enum POCOs exist, yet there is no DbContext, no mappings or constraints, no migration, no seed data, no repository/transaction boundary, and no identity hashing. E02 turns that contract into a real, SQL-backed schema that downstream epics (E04 auth, E05 fetch lifecycle, E08 atomic upsert) depend on.

## What Changes

- Harden the domain model: enums already match §3, but entities get constructor/command-level invariants so invalid enum/domain states (for example, out-of-range `port`, undefined enum values, non-UTC timestamps, and blank required identifiers) cannot be created through normal paths.
- Add an EF Core `DbContext` with lower-camel plural table mappings, nullability, relationships, UTC timestamps, and enum conversions matching §4.
- Add the full set of schema constraints and indexes: both unique identities (`(providerId, iocType, iocValueHash, port)` and `(providerId, providerRecordId)`), hash/search/relation/history indexes, `port`/`threatFoxConfidenceLevel` CHECK ranges, and `phishTankDetails` ISJSON validation, including correct SQL Server behavior for NULL `port` in the unique index.
- Create the initial migration and idempotent seed of the two provider rows (ThreatFox id 1 / 5 min, PhishTank id 2 / 8 h, both `nextFetchTime = NULL`).
- Prepare the first-admin seed workflow behind a password-hasher abstraction. E04 supplies the production password KDF and activates the workflow at startup; E02 verifies the seed rules with a test implementation so persistence does not own authentication cryptography.
- Add repository abstractions (grouped reads, identity resolution) and a unit-of-work transaction boundary, plus a SHA-256 value-hashing service for index-safe IOC identity.

## Capabilities

### New Capabilities
- `domain-model`: entities and enums implementing the provider-attributed IOC model, with invariants that prevent invalid domain states.
- `persistence`: EF Core mappings, constraints, indexes, and the initial migration that materialize the §4 schema.
- `seeding`: idempotent provider-row seeds with deterministic defaults.
- `admin-seed`: deterministic first-admin seed rules and persistence flow, activated once E04 provides the production password hasher.
- `identity-hashing`: SHA-256 hashing of normalized IOC values for index-safe provider identity.
- `repositories`: repository abstractions and the transaction (unit-of-work) boundary used by ingestion and query paths.

### Modified Capabilities
- None — `openspec/specs/` is empty; all capabilities are new.

## Impact

- `src/ThreatIntelligence.Domain` — entities/enums hardened with invariants.
- `src/ThreatIntelligence.Infrastructure` — new `Persistence` area: DbContext, configurations, migrations, seeds, repositories, hashing service.
- `src/ThreatIntelligence.Application` — repository abstractions shared with query handlers.
- `src/ThreatIntelligence.API` / `src/ThreatIntelligence.Worker` — startup wiring for migrations and provider seeds; E04 later adds first-admin activation after registering the production password hasher.
- `tests/ThreatIntelligence.UnitTests` and `tests/ThreatIntelligence.IntegrationTests` — new SQL-backed tests for constraints, seeds, transactions, and hash vectors.
- `docker-compose.yml` already provides `mssql` (1433) and the `testsqlserver` profile (1432); no Compose change required for this epic.
- No public API or message-contract changes; this is a foundation epic consumed by E04–E09.
