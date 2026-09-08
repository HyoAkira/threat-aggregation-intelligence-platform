## Purpose

Defines the common provider-owned IOC and normalized tag persistence schema
used by threat-intelligence ingestion sources. API keys are managed outside the
database.

## Requirements

### Requirement: Provider configuration table stores source metadata

The system SHALL store each provider in the `providers` table with an integer primary key, required name and base URL, enabled state, and fetch interval in minutes. Provider API keys SHALL NOT be stored in this table.

#### Scenario: Provider rows are queryable
- **WHEN** the application resolves a configured ingestion source
- **THEN** the matching provider row supplies its identity and source metadata
- **AND** the identity can own IOC and tag associations

### Requirement: Provider records use a common IOC model

The system SHALL store every accepted ThreatFox and PhishTank record as an `iocs` row owned by exactly one provider. Each row SHALL retain the normalized IOC value and type, its SHA-256 value hash, provider-native record identifier, applicable provider metadata, source timestamps, creation timestamp, and last successful update timestamp. Provider-inapplicable values SHALL remain null rather than receiving synthetic defaults.

#### Scenario: ThreatFox record is stored in the common model
- **WHEN** the ThreatFox ingestion operation accepts a provider record
- **THEN** the system stores or updates a provider-owned `iocs` row
- **AND** applicable ThreatFox metadata remains attributed to that row
- **AND** no row is written to a provider-specific ThreatFox table

#### Scenario: PhishTank record is stored in the common model
- **WHEN** the PhishTank ingestion operation accepts a provider record
- **THEN** the system stores or updates a provider-owned `iocs` row
- **AND** applicable PhishTank metadata and complete validated details remain attributed to that row
- **AND** no row is written to a provider-specific PhishTank table

### Requirement: Re-fetching a provider does not create duplicate rows

The system SHALL enforce provider-scoped uniqueness on `(providerId, iocType, iocValueHash, port)` and separately on `(providerId, providerRecordId)`. A re-fetch SHALL update the single matching provider-owned IOC row instead of inserting a duplicate, preserve its creation timestamp, and refresh its update timestamp and current provider metadata.

#### Scenario: Duplicate ThreatFox entry is rejected
- **WHEN** ThreatFox ingestion receives a record matching an existing ThreatFox-owned IOC identity
- **THEN** the existing common IOC row is updated
- **AND** no second row is created

#### Scenario: Duplicate PhishTank entry is rejected
- **WHEN** PhishTank ingestion receives a record matching an existing PhishTank-owned IOC identity
- **THEN** the existing common IOC row is updated
- **AND** no second row is created

#### Scenario: Existing provider IOC is upserted
- **WHEN** a normalized provider record matches an existing row by its provider-scoped identity
- **THEN** the existing row is updated
- **AND** no second provider IOC row is created

#### Scenario: Equivalent IOC from another provider remains source-attributed
- **WHEN** two providers report the same normalized IOC value and type
- **THEN** each provider retains its own IOC row
- **AND** the uniqueness constraints permit both rows

### Requirement: Provider tags are normalized and source-attributed

The system SHALL trim and lowercase provider tag names, store each distinct name once in `tags`, and associate tags with their provider-owned IOC through `iocTags`. A successful re-fetch SHALL replace that IOC row's tag associations with the provider's current tag set.

#### Scenario: ThreatFox tags are refreshed
- **WHEN** an existing ThreatFox IOC is re-fetched with a changed tag set
- **THEN** its previous `iocTags` associations are replaced by the current normalized set
- **AND** tag records shared by other IOC rows remain available

### Requirement: API keys are sourced from user-secrets, not the database

The system SHALL NOT store provider API keys in any database table. API keys
(`AuthKey__AbuseCh` for ThreatFox, `PhishTank__ApiKey`) SHALL be read from
`dotnet user-secrets` (local dev) or environment variables / `.env` (Docker)
at runtime.

#### Scenario: Provider fetch uses user-secrets API key
- **WHEN** a provider fetcher executes
- **THEN** it reads the API key from configuration (`AuthKey__AbuseCh` or
  `PhishTank__ApiKey`)
- **AND** no API key value is ever persisted to the database

### Requirement: Entity types in migration metadata reference Domain layer

The system SHALL reference the `Provider`, `Ioc`, `Tag`, and `IocTag` entities by their fully-qualified `ThreatIntelligence.Domain.Entities` type names in EF Core migration metadata.

#### Scenario: ModelSnapshot resolves entity types from Domain.Entities
- **WHEN** the current EF Core model snapshot is inspected
- **THEN** it references `Provider`, `Ioc`, `Tag`, and `IocTag` under `ThreatIntelligence.Domain.Entities`
- **AND** it does not reference the removed provider-specific entity types

#### Scenario: Migration designer files use correct type names
- **WHEN** the common-schema migration metadata is inspected
- **THEN** its entity type names match the common Domain entity locations

#### Scenario: Migration metadata resolves common model entities
- **WHEN** `ThreatIntelDbContext` initializes its EF Core model
- **THEN** migration metadata resolves every common persistence entity from the Domain layer
- **AND** it contains no entity mapping for the removed `ThreatFox` or `PhishTank` types
