## Purpose

Defines the provider-attributed IOC domain model — the entities, enumerations, and creation invariants that make invalid domain states impossible to construct through normal application paths.

## ADDED Requirements

### Requirement: Enumeration values match the data contract
The domain SHALL define byte-backed enumerations with the exact ordinal values from the data contract:
- `IocType`: Ipv4=0, Ipv6=1, Domain=2, Url=3.
- `FetchTrigger`: Scheduled=0, Manual=1.
- `FetchMode`: Recent=0, Full=1.
- `FetchStatus`: Queued=0, Running=1, Success=2, Failed=3.
- `UserRole`: User=0, Admin=1.

#### Scenario: Enumerations expose the documented ordinals
- **WHEN** an application reads the domain enumerations
- **THEN** each enum exposes exactly the documented name-to-ordinal mapping

#### Scenario: A store rejects out-of-range enum values
- **WHEN** a value outside an enumeration's defined range is assigned to an entity property
- **THEN** the entity refuses to construct or save that state

### Requirement: Provider entity invariants
A `Provider` SHALL require a non-empty display/integration name and a non-empty base URL, an interval of at least one minute, and SHALL expose fetch-status summary, last-fetch, and next-fetch times as nullable values.

#### Scenario: Provider with blank name or base URL is rejected
- **WHEN** a Provider is created with an empty name or base URL
- **THEN** construction fails and no invalid Provider exists

#### Scenario: Provider with a non-positive interval is rejected
- **WHEN** a Provider is created with an interval less than one minute
- **THEN** construction fails and no invalid Provider exists

### Requirement: IOC entity invariants
An `IoC` SHALL require a non-empty provider-record identifier (max 100 characters) and a non-empty normalized IOC value (max 2048 characters), SHALL carry a 32-byte value hash, SHALL constrain `port` to NULL or 1–65535, SHALL constrain `threatFoxConfidenceLevel` to NULL or 0–100, and SHALL record creation and update timestamps as UTC.

#### Scenario: Blank provider record ID or IOC value is rejected
- **WHEN** an IoC is created with a blank provider-record ID or blank IOC value
- **THEN** construction fails and no invalid IoC exists

#### Scenario: IOC with an out-of-range port is rejected
- **WHEN** an IoC is created with a port outside 1–65535
- **THEN** construction fails and no invalid IoC exists

#### Scenario: IOC with an out-of-range confidence level is rejected
- **WHEN** an IoC is created with a `threatFoxConfidenceLevel` outside 0–100
- **THEN** construction fails and no invalid IoC exists

#### Scenario: IOC with a malformed value hash is rejected
- **WHEN** an IoC is created with a value hash that is not exactly 32 bytes
- **THEN** construction fails and no invalid IoC exists

### Requirement: Provider-inapplicable fields are nullable
ThreatFox-only, PhishTank-only, and relation fields on an `IoC` SHALL be nullable so a provider-inapplicable value is NULL while valid `false`, `0`, and empty values remain stored non-null.

#### Scenario: Valid false and zero values are preserved
- **WHEN** a provider supplies an explicit `false` or `0` value for a field
- **THEN** that value is stored as `false`/`0`, never converted to NULL

### Requirement: Fetch history entity shape
`FetchHistory` SHALL carry a unique logical job identifier, the owning provider, trigger, mode, started and finished timestamps, a fetch status, added/updated record counters, and an optional terminal error detail.

#### Scenario: Fetch history records all lifecycle fields
- **WHEN** a fetch attempt is recorded
- **THEN** the history row exposes job id, provider, trigger, mode, started/finished times, status, counters, and optional error detail

### Requirement: Fetch history state invariants
`FetchHistory` SHALL require a non-empty job ID, a valid provider identity, defined trigger/mode/status values, a UTC `startedAt`, an optional UTC `finishedAt` not earlier than `startedAt`, and non-negative counters. Queued and Running states SHALL have no finish time or terminal error. Success SHALL have a finish time and no terminal error. Failed SHALL have a finish time, a non-blank terminal error, and zero added/updated counters.

#### Scenario: Active state rejects terminal fields
- **WHEN** a Queued or Running history is constructed with a finish time or terminal error
- **THEN** construction fails

#### Scenario: Success requires completion
- **WHEN** a Success history lacks a valid finish time or contains a terminal error
- **THEN** construction fails

#### Scenario: Failure requires zero counters and an error
- **WHEN** a Failed history has a positive counter, no finish time, or a blank terminal error
- **THEN** construction fails

### Requirement: User, setting, and tag invariants
`User` SHALL reject blank or over-50-character usernames, blank or over-256-character password hashes, undefined roles, and non-UTC creation timestamps. `Settings` SHALL reject blank or over-100-character keys and non-UTC update timestamps. `Tag` SHALL trim and lowercase names, reject blank or over-100-character normalized names, and expose the normalized name used for uniqueness. `IoCTag` SHALL require valid positive IOC and tag identities.

#### Scenario: Invalid user is rejected
- **WHEN** a User has an invalid username, password hash, role, or creation timestamp
- **THEN** construction fails and no invalid User exists

#### Scenario: Invalid setting is rejected
- **WHEN** a Setting has an invalid key or update timestamp
- **THEN** construction fails and no invalid Setting exists

#### Scenario: Tag is normalized
- **WHEN** a Tag is created from `  BotNet  `
- **THEN** its stored name is `botnet`

#### Scenario: Invalid IOC-tag link is rejected
- **WHEN** an IoCTag is created with a non-positive IOC or tag identity
- **THEN** construction fails and no invalid IoCTag exists
