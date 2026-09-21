## Purpose

Proves schema, persistence, ingestion, grouped-query, and authenticated HTTP behavior against an isolated supported SQL Server rather than an in-memory substitute.

## ADDED Requirements

### Requirement: Isolated real-SQL harness
SQL verification SHALL use a supported SQL Server with a unique database per run or worker and reliable cleanup. Parallel jobs SHALL NOT reset or share another job's database, and EF InMemory SHALL NOT be used as evidence for SQL behavior.

#### Scenario: SQL suites run in parallel
- **WHEN** two CI workers execute integration suites concurrently
- **THEN** each uses a distinct database and neither deletes or changes the other's data

### Requirement: Schema, migration, and seed verification
Tests SHALL prove empty installation, ordered migration application, idempotency at the latest schema, and upgrade from each supported predecessor once more than one migration exists. Every documented constraint and index SHALL be checked; rejected writes SHALL assert the expected SQL error number and named constraint or index when SQL Server exposes it. Concurrent initialization SHALL converge without duplicate seeds.

#### Scenario: Duplicate identity violates the intended index
- **WHEN** a write duplicates a documented provider identity
- **THEN** SQL Server rejects it through the expected unique index rather than an unrelated failure

#### Scenario: Concurrent initialization
- **WHEN** two initializers target an empty or upgradeable database
- **THEN** migration and seed work converges on the latest schema with exactly one row per deterministic seed

### Requirement: Transaction, upsert, and query verification
Real-SQL tests SHALL prove whole-batch rollback, provider identity conflicts, repeat and concurrent upserts, exact counters and timestamps, tag replacement, source attribution, grouped exact/detail/browse/relations/summary behavior, aggregate null/tie ordering, and stable bounded pagination. Representative query evidence SHALL confirm intended indexes are usable.

#### Scenario: Invalid final record rolls back prior chunks
- **WHEN** the last record is invalid after earlier chunks were processed
- **THEN** no IOC/tag writes remain and the logical history is Failed with zero added and updated counters

#### Scenario: Tied pages remain stable
- **WHEN** repeated page requests operate on groups with tied aggregate sort values
- **THEN** pages return deterministic disjoint identities without skips or duplicates

### Requirement: Authenticated API verification on SQL persistence
HTTP integration tests SHALL cover admin seeding/login, JWT and User/Admin authorization, every E09 query endpoint, and every E10 Admin endpoint using real SQL persistence. Responses and persistence effects SHALL match their public contracts.

#### Scenario: Authorization and persistence matrix
- **WHEN** anonymous, User, and Admin clients exercise public and Admin endpoints
- **THEN** 401, 403, and successful outcomes match policy and successful operations read or change the expected SQL state
