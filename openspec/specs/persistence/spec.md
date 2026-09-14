## Purpose

Materializes the data-contract schema in SQL Server through EF Core mappings, constraints, and indexes, and provides a migration that builds the schema on an empty database and upgrades existing ones.

## Requirements

### Requirement: Table and column naming
All persisted table names SHALL use plural lower-camel case (for example, `iocs`, `providers`, and `fetchHistory`). All persisted column names SHALL use lower-camel case (for example, `iocValueHash`).

#### Scenario: Generated schema uses documented names
- **WHEN** the schema is generated from the mappings
- **THEN** every table and column is named in plural lower-camel case as documented in the data contract

### Requirement: Column types, lengths, and nullability match the contract
The schema SHALL map each column to the type, maximum length, and nullability documented in the data contract, including `binary(32)` for the IOC value hash, `nvarchar(max)` for `phishTankDetails` and `errorDetail`, and `datetime2` for all timestamps. Provider-inapplicable fields SHALL be nullable.

#### Scenario: Generated column metadata matches the contract
- **WHEN** the generated schema is inspected
- **THEN** each column has the documented type, length, and nullability

### Requirement: UTC timestamps
All timestamp columns SHALL be stored and compared in UTC.

#### Scenario: Timestamps round-trip in UTC
- **WHEN** an entity with UTC timestamps is persisted and reloaded
- **THEN** the values round-trip without timezone conversion or drift

### Requirement: Provider-scoped identity uniqueness
The schema SHALL enforce both provider identities as unique. The logical endpoint identity is `(providerId, iocType, iocValueHash, port)` with NULL ports treated as equal. SQL Server SHALL implement that rule with a persisted computed `portKey = COALESCE(port, 0)` and a unique index on `(providerId, iocType, iocValueHash, portKey)`. The provider-native identity is `(providerId, providerRecordId)`.

#### Scenario: Duplicate provider endpoint rows are rejected
- **WHEN** two rows with the same provider, IOC type, value hash, and non-null port are inserted
- **THEN** the second insert fails with a unique-constraint violation

#### Scenario: Duplicate provider record IDs are rejected
- **WHEN** two rows for the same provider with the same provider-record ID are inserted
- **THEN** the second insert fails with a unique-constraint violation

#### Scenario: Same normalized IOC from different providers is allowed
- **WHEN** two providers each insert a row with the same IOC type and value hash
- **THEN** both inserts succeed because identity is provider-scoped

### Requirement: NULL port uniqueness behavior in SQL Server
The provider-scoped endpoint identity SHALL treat NULL port as a single bucket: for a given provider, IOC type, and value hash, at most one row MAY have a NULL port, rows with an equal non-null port remain unique, and rows with different non-null ports remain distinct source entries.

#### Scenario: Duplicate NULL-port rows are rejected
- **WHEN** two rows for the same provider, type, and hash both have NULL port
- **THEN** the second insert fails with a unique-constraint violation

#### Scenario: Two rows with the same non-null port are rejected
- **WHEN** two rows for the same provider, type, and hash both carry the same non-null port
- **THEN** the second insert fails with a unique-constraint violation

#### Scenario: Two rows with different non-null ports are allowed
- **WHEN** two rows for the same provider, type, and hash carry different non-null ports (e.g. 80 and 443)
- **THEN** both inserts succeed as separate source entries

### Requirement: Search, relation, and history indexes
The schema SHALL create the documented supporting indexes: `(iocType, iocValueHash)` for grouping, `(iocType, urlHost)` and `(iocType, parentDomain)` for relations, `(lastSeenUtc)` for list ordering, `(providerId, startedAt)` on `fetchHistory`, and unique indexes on `fetchHistory.jobId`, `users.username`, `settings.key`, and `tags.name`.

#### Scenario: Indexes exist in the generated schema
- **WHEN** the generated schema is inspected
- **THEN** every documented index is present

#### Scenario: Duplicate logical job ID is rejected
- **WHEN** two fetch-history rows are inserted with the same `jobId`
- **THEN** the second insert fails with a unique-constraint violation

### Requirement: Check constraints
The schema SHALL enforce the documented check constraints: `port` is NULL or between 1 and 65535, `threatFoxConfidenceLevel` is NULL or between 0 and 100, and `phishTankDetails` is NULL or valid JSON (`ISJSON(phishTankDetails) = 1`).

#### Scenario: Out-of-range port is rejected by the database
- **WHEN** a row with a port outside 1–65535 is inserted directly
- **THEN** the insert fails with a check-constraint violation

#### Scenario: Out-of-range confidence level is rejected by the database
- **WHEN** a row with a confidence level outside 0–100 is inserted directly
- **THEN** the insert fails with a check-constraint violation

#### Scenario: Invalid JSON details are rejected by the database
- **WHEN** a row with `phishTankDetails` that is not valid JSON is inserted directly
- **THEN** the insert fails with a check-constraint violation

### Requirement: Enum conversions are persisted by ordinal
Enum-valued columns SHALL be persisted as their ordinal `tinyint` values, never as strings.

#### Scenario: Enum values persist as ordinals
- **WHEN** an entity with enum properties is persisted
- **THEN** the database stores the ordinal values and reloads the original enum values

### Requirement: Initial migration applies deterministically
The initial migration SHALL create the full schema on an empty database and SHALL be safe to apply again without data loss or errors.

#### Scenario: Migration succeeds on an empty database
- **WHEN** the initial migration is applied to an empty SQL Server database
- **THEN** all documented tables, constraints, and indexes are created and the migration records a single clean application

#### Scenario: Migration is idempotent on an existing database
- **WHEN** the migration set is applied to an already-migrated database
- **THEN** no rows are lost and no duplicate objects or errors occur

### Requirement: Provider configuration columns
The `providers` table SHALL persist provider fetch configuration: `defaultMode` as a non-null `tinyint` ordinal, `supportedModes` as a non-null integer mode bitmask, `recentCooldownMinutes` and `fullCooldownMinutes` as nullable integers, and `minimumIntervalMinutes` as a non-null integer.

#### Scenario: Generated schema carries provider configuration
- **WHEN** the generated schema is inspected
- **THEN** the `providers` table has the documented configuration columns with the documented types and nullability

### Requirement: Provider configuration migration
A migration SHALL add the provider configuration columns to an existing `providers` table without data loss and SHALL backfill the seeded ThreatFox and PhishTank rows with their documented capability, cooldown, and minimum-interval defaults.

#### Scenario: Existing database is upgraded without data loss
- **WHEN** the migration set is applied to a database that already contains providers
- **THEN** the configuration columns are added and no existing provider row is lost

#### Scenario: Seeded rows are backfilled
- **WHEN** the migration runs against a database that already contains the seeded providers
- **THEN** ThreatFox and PhishTank receive their documented default mode, supported modes, cooldowns, and minimum intervals