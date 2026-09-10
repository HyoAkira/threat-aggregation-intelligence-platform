## Purpose

Makes schema migration and deterministic seeding a single-owner deployment gate with safe forward-only behavior and observable failure.

## ADDED Requirements

### Requirement: Single migration and seed owner
A dedicated one-shot deployment process SHALL be the sole production caller of database migration and deterministic provider/admin seeding. API and Worker SHALL NOT race migration or seed work during normal production startup, and both SHALL start only after the deployment process succeeds.

#### Scenario: Empty database starts
- **WHEN** the stack targets an empty database
- **THEN** one migration process creates the latest schema, writes each deterministic seed once, exits successfully, and then application hosts start

#### Scenario: Existing database upgrades
- **WHEN** pending migrations exist on a supported predecessor schema
- **THEN** they apply in order once and an already-current rerun changes no schema or seed data

### Requirement: Concurrent migrator protection
Migration and seed execution SHALL acquire a SQL Server application lock or equivalent database-owned exclusive lock. Concurrent invocations SHALL serialize, SHALL NOT duplicate seeds, and SHALL report applied migration identifiers and outcome without secrets.

#### Scenario: Two migrators start
- **WHEN** two deployment processes target the same database
- **THEN** only one performs pending work while the other waits or exits safely after observing the current schema

### Requirement: Forward-only schema policy
Schema deployment SHALL be forward-only by default. Before a release with pending migrations, the operator SHALL create and verify a database backup. A destructive or backward-incompatible change SHALL document restoration impact; application rollback SHALL NOT automatically run EF down migrations.

#### Scenario: Old application is schema-incompatible
- **WHEN** rollback evaluation finds the previous application cannot use the migrated schema
- **THEN** the documented quiesced database-restore path is required instead of automatic downgrade
