## Purpose

Defines the canonical threat-IOC data model and the persistence contracts the
backend relies on: a globally-deduplicated `Ioc`, its per-provider `IocSource`
sightings, the `Provider` source registry, plus `FetchHistory` and `LogEntry`
audit tables.

## ADDED Requirements

### Requirement: IOC is uniquely keyed by type and normalized value

The system SHALL treat an IOC as a single canonical record and SHALL NOT store
two `Ioc` records that share the same `(IocType, NormalizedValue)` pair. An
attempt to insert a duplicate SHALL reuse the existing record.

#### Scenario: Duplicate IOC from the same source collapses to one record
- **WHEN** a fetch inserts an IOC whose `(IocType, NormalizedValue)` already exists
- **THEN** no new `Ioc` row is created
- **AND** a new `IocSource` sighting row is recorded under the originating provider

#### Scenario: Lookup returns exactly one canonical IOC
- **WHEN** a consumer looks up an IOC by `(IocType, NormalizedValue)`
- **THEN** exactly one `Ioc` record is returned

### Requirement: Each IOC tracks its source sightings

The system SHALL retain, for each `Ioc`, the set of providers that have reported
that IOC, together with per-provider `FirstSeen` and `LastSeen` timestamps, the
un-normalized `RawValue` as it appeared at that source, and any provider-specific
metadata.

#### Scenario: Provider sighting retains first/last seen and raw value
- **WHEN** a provider reports an IOC with a raw value and a first-seen timestamp
- **THEN** an `IocSource` row is created linking that provider to the `Ioc`
- **AND** the row records `FirstSeen`, `LastSeen`, and `RawValue`

#### Scenario: Repeat sightings from the same provider extend last-seen
- **WHEN** the same provider reports the same IOC again in a later fetch
- **THEN** the existing `IocSource` row is reused
- **AND** its `LastSeen` is advanced to the new sighting time

### Requirement: Threat source providers are seeded

The system SHALL seed the `Provider` registry with at least ThreatFox,
PhishTank, and URLhaus as known, toggleable sources, each declaring which IOC
types (`IPv4`, `Domain`, `URL`) it supplies.

#### Scenario: Seed providers are present after initialization
- **WHEN** the database is initialized
- **THEN** `Provider` records for ThreatFox, PhishTank, and URLhaus exist
- **AND** each is marked enabled by default

### Requirement: Each fetch run is audited

The system SHALL record a `FetchHistory` row for every provider fetch run,
storing its start/completion time, outcome (success/failure), the number of
items fetched and upserted, and any error message.

#### Scenario: A scheduled fetch run is recorded
- **WHEN** a provider fetch completes
- **THEN** a `FetchHistory` row records `StartedAt`, `CompletedAt`, `Status`,
  `CountFetched`, `CountUpserted`, and a nullable `Error`

### Requirement: Application log entries are queryable

The system SHALL persist application `LogEntry` records (timestamp, level,
category, message, optional exception) so they can be listed for observability.

#### Scenario: An error during fetch is logged and retrievable
- **WHEN** an exception is thrown during a provider fetch
- **THEN** a `LogEntry` with level Error is persisted
- **AND** subsequent listing of log entries includes that record
