## Purpose

Defines the persistence read/write abstractions and the unit-of-work transaction boundary used by ingestion and query paths, including the rule that fetch-history is written outside the IOC transaction.

## ADDED Requirements

### Requirement: Grouped and identity reads
The persistence layer SHALL support grouped reads of normalized IOC rows and SHALL resolve rows by both provider-scoped identities: `(providerId, iocType, iocValueHash, port)` and `(providerId, providerRecordId)`.

#### Scenario: Row resolved by endpoint identity
- **WHEN** a lookup is made for a provider, IOC type, value hash, and port
- **THEN** the existing row for that endpoint identity is returned or a not-found result is returned

#### Scenario: Row resolved by provider record ID
- **WHEN** a lookup is made for a provider and provider-record ID
- **THEN** the existing row with that provider-native ID is returned or a not-found result is returned

### Requirement: Unit-of-work commit and rollback boundaries
The persistence layer SHALL expose a unit of work where changes made within a transaction commit atomically or roll back entirely, leaving no partial writes on failure.

#### Scenario: Commit persists all changes
- **WHEN** a unit of work commits successfully
- **THEN** all changes made within the transaction are persisted

#### Scenario: Rollback discards all changes
- **WHEN** a unit of work fails or is rolled back
- **THEN** none of the changes made within the transaction are persisted

### Requirement: Fetch history is written outside the IOC transaction
Fetch-history rows SHALL be persisted independently of the IOC transaction so a failed IOC batch still records its failed history row.

#### Scenario: Failed batch still records failed history
- **WHEN** an IOC batch fails and is rolled back
- **THEN** a separate failed history row with zero added and zero updated counts remains persisted with the error detail

#### Scenario: Successful batch records committed counters
- **WHEN** an IOC batch commits successfully
- **THEN** the separate history row records the committed added and updated counts