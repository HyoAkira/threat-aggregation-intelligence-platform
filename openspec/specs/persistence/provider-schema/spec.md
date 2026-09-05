## Purpose

Defines the `providers` database schema and the typed tables for storing raw
threat-intelligence API responses from each source (PhishTank, ThreatFox).
Each provider's data is stored in its own table with typed columns and a
unique constraint to prevent duplicate inserts on re-fetch. API keys are
managed via user-secrets, never stored in the database.

## ADDED Requirements

### Requirement: Provider configuration table stores source metadata

The system SHALL store `Provider` rows in a `threat.Provider` table (threat schema) with
at least `ProviderId` (Guid, PK), `Name` (string), `BaseUrl` (string),
`IsEnabled` (bool), and `FetchIntervalMinutes` (int). API keys are NOT stored
in this table — they are read from user-secrets at runtime.

#### Scenario: Provider rows are queryable
- **WHEN** the application queries `ThreatIntelDbContext.Providers`
- **THEN** all configured provider rows are returned
- **AND** each row has `Name`, `BaseUrl`, `IsEnabled`, `FetchIntervalMinutes` populated

### Requirement: ThreatFox raw responses are stored in a typed table

The system SHALL store ThreatFox API response entries in a table named
`ThreatFox` under the `providers` schema. Each row represents a single IOC
entry from the API response, with typed columns for every field in the
response: `IocId` (int), `IocValue` (nvarchar(500)), `IocType` (nvarchar(50)),
`ThreatType` (nvarchar(100)), `Malware` (nvarchar(200)), `MalwareAlias`
(nvarchar(500), nullable), `MalwarePrintable` (nvarchar(200)), `FirstSeenUtc`
  (datetime2), `LastSeenUtc` (datetime2, nullable), `ConfidenceLevel` (int),
  `IsCompromised` (bit), `Reference` (nvarchar(max), nullable), `Tags`
  (nvarchar(max), nullable), `Anonymous` (int), `Reporter` (nvarchar(200)).

#### Scenario: ThreatFox response entries with long reference URLs are persisted
- **WHEN** a ThreatFox API response contains an IOC entry with a Reference URL exceeding 1000 characters
- **THEN** the entry is inserted as a row in `providers.ThreatFox`
- **AND** the full Reference URL is stored without truncation
- **AND** all other typed columns are stored correctly

### Requirement: PhishTank raw responses are stored in a typed table

The system SHALL store PhishTank API response entries in a table named
`PhishTank` under the `providers` schema. Each row represents a single phishing
entry with typed columns: `PhishId` (int), `Url` (nvarchar(max)),
`PhishDetailUrl` (nvarchar(max)), `SubmissionTime` (datetime2), `Verified`
(bit — parsed from the API's `'yes'/'no'` string), `VerificationTime`
(datetime2, nullable), `Online` (bit — parsed from `'yes'/'no'`), `Target`
(nvarchar(500), nullable), `Details` (nvarchar(max), nullable — JSON array of
the nested details objects).

#### Scenario: PhishTank response entries are persisted as typed rows
- **WHEN** a PhishTank API response contains phish entries
- **THEN** each entry is inserted as a row in `providers.PhishTank`
- **AND** `Verified` and `Online` string values are converted to `bit`
- **AND** `Details` (the nested array of IP/cidr objects) is stored as JSON

### Requirement: Re-fetching a provider does not create duplicate rows

The system SHALL enforce unique constraints to prevent duplicate inserts when
a provider's data is re-fetched:
- `providers.ThreatFox`: unique on `(IocId, IocValue, IocType)`
- `providers.PhishTank`: unique on `PhishId`

#### Scenario: Duplicate ThreatFox entry is rejected
- **WHEN** a ThreatFox entry with the same `(IocId, IocValue, IocType)` is
  inserted again
- **THEN** the database unique constraint prevents the duplicate insert
- **AND** no second row is created

#### Scenario: Duplicate PhishTank entry is rejected
- **WHEN** a PhishTank entry with the same `PhishId` is inserted again
- **THEN** the database unique constraint prevents the duplicate insert
- **AND** no second row is created

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
