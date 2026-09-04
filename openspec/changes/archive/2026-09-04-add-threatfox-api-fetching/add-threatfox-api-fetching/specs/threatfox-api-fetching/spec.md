## Purpose

Fetches IOCs from the ThreatFox Export API (abuse.ch) and persists them to the `providers.ThreatFox` table with upsert semantics.

## ADDED Requirements

### Requirement: ThreatsFox API service can download the full IOC dataset

The system SHALL provide a service that can fetch the full ThreatFox IOC dataset by downloading the `full.json.zip` file from `https://threatfox-api.abuse.ch/v2/files/exports/{AuthKey__AbuseCh}/full.json.zip`, decompressing the ZIP archive, parsing the JSON array of IOC objects, and upserting each entry into `providers.ThreatFox`.

#### Scenario: Full dataset download succeeds
- **WHEN** the service is invoked with a valid API key configured
- **THEN** the service downloads the ZIP file containing `full.json`
- **AND** the JSON array is parsed into IOC records
- **AND** each record is upserted into `providers.ThreatFox` (insert or update on conflict for `(IocId, IocValue, IocType)`)
- **AND** the number of records processed is returned

#### Scenario: API key is not configured
- **WHEN** the service is invoked and `AuthKey__AbuseCh` is null or empty
- **THEN** an `InvalidOperationException` is thrown with a message indicating the key is not configured

#### Scenario: API returns HTTP error
- **WHEN** the ThreatFox API returns a non-success status code
- **THEN** an `HttpRequestException` is thrown containing the status code and error body

#### Scenario: ZIP archive does not contain full.json
- **WHEN** the downloaded ZIP archive does not contain an entry named `full.json`
- **THEN** an `InvalidDataException` is thrown

#### Scenario: API returns empty or malformed JSON
- **WHEN** the parsed JSON is not an array or is empty
- **THEN** an exception is thrown

### Requirement: ThreatFox API service can fetch recent IOCs

The system SHALL provide a service that can fetch recent ThreatFox IOCs (last 48 hours) by downloading the `recent.json` file from `https://threatfox-api.abuse.ch/v2/files/exports/{AuthKey__AbuseCh}/recent.json`, parsing the JSON array of IOC objects, and upserting each entry into `providers.ThreatFox`.

#### Scenario: Recent IOCs fetch succeeds
- **WHEN** the service is invoked with a valid API key configured
- **THEN** the service downloads `recent.json` directly (no ZIP decompression needed)
- **AND** the JSON array is parsed into IOC records
- **AND** each record is upserted into `providers.ThreatFox` (insert or update on conflict for `(IocId, IocValue, IocType)`)
- **AND** the number of records processed is returned

#### Scenario: API returns HTTP error for recent IOCs
- **WHEN** the ThreatFox API returns a non-success status code
- **THEN** an `HttpRequestException` is thrown containing the status code and error body

### Requirement: ThreatFox IOC records are upserted on conflict

The system SHALL upsert IOC records into `providers.ThreatFox` using the unique constraint on `(IocId, IocValue, IocType)`. When a record with the same `(IocId, IocValue, IocType)` already exists, all other columns are updated; when no matching record exists, a new row is inserted.

#### Scenario: New IOC record is inserted
- **WHEN** an IOC entry is fetched with a new `(IocId, IocValue, IocType)`
- **THEN** a new row is inserted into `providers.ThreatFox`

#### Scenario: Existing IOC record is updated
- **WHEN** an IOC entry is fetched with the same `(IocId, IocValue, IocType)` as an existing row
- **THEN** the existing row is updated with the new column values
- **AND** no duplicate row is created

### Requirement: IOC JSON fields map to ThreatFox entity columns

The ThreatFox API Export JSON response is an **object** keyed by `ioc_id` (string), with each value being an **array** of IOC objects. The system SHALL map the following JSON response fields to the `ThreatFox` entity columns:

| JSON Field | Entity Column |
|---|---|
| JSON object key (e.g. `"1894343"`) | `IocId` (int, parsed from string key) |
| `ioc_value` | `IocValue` (nvarchar(500)) |
| `ioc_type` | `IocType` (nvarchar(50)) |
| `threat_type` | `ThreatType` (nvarchar(100)) |
| `malware` | `Malware` (nvarchar(200)) |
| `malware_alias` | `MalwareAlias` (nvarchar(500), nullable) |
| `malware_printable` | `MalwarePrintable` (nvarchar(200)) |
| `first_seen_utc` | `FirstSeenUtc` (datetime2) |
| `last_seen_utc` | `LastSeenUtc` (datetime2, nullable) |
| `confidence_level` | `ConfidenceLevel` (int) |
| `is_compromised` | `IsCompromised` (bit) |
| `reference` | `Reference` (nvarchar(1000), nullable) |
| `tags` | `Tags` (nvarchar(max), nullable) |
| `anonymous` | `Anonymous` (int) |
| `reporter` | `Reporter` (nvarchar(200)) |

#### Scenario: JSON fields are correctly mapped
- **WHEN** a JSON object with keys like `"1894343"` containing an array of IOCs is parsed
- **THEN** the object key is parsed as `IocId`
- **AND** each IOC in the inner array has its fields mapped to the corresponding entity properties
- **AND** optional fields (`MalwareAlias`, `LastSeenUtc`, `Reference`, `Tags`) are handled if `null`

### Requirement: ThreatFox API fetching is triggered manually via HTTP endpoint

The system SHALL provide an HTTP endpoint that allows a user to manually trigger ThreatFox IOC fetching. The endpoint SHALL use authentication.

#### Scenario: Full dataset download endpoint
- **WHEN** `GET /api/threatfox/download` is called
- **THEN** the service fetches the full dataset
- **AND** the response contains the number of IOCs processed

#### Scenario: Recent IOCs endpoint
- **WHEN** `GET /api/threatfox/recent` is called
- **THEN** the service fetches recent IOCs
- **AND** the response contains the number of IOCs processed

### Requirement: API key is sourced from .env as single source for both local and docker-compose

The system SHALL read the ThreatFox API key from the `AuthKey:AbuseCh` configuration key at runtime (note: colon-separated, NOT double underscore, because .NET's `AddEnvironmentVariables()` converts `__` to `:` when storing env vars in configuration). The key is sourced from the `.env` file which serves as the single source of truth for both local development and docker-compose deployment. For local development, the `.env` file is loaded into environment variables via a custom `LoadEnvFile()` helper in `Program.cs` that runs BEFORE `WebApplication.CreateBuilder()`. Conflicting user-secrets are removed to prevent override. For docker-compose, the `.env` values are mapped to container environment variables via `docker-compose.yml`. The key is NEVER stored in the database.

#### Scenario: API key is read from .env
- **WHEN** the ThreatFox API service makes an HTTP request
- **THEN** it reads `AuthKey:AbuseCh` from `IConfiguration` (populated from `.env` via environment variables)
- **AND** the key is embedded in the URL path as `/v2/files/exports/{key}/...`

#### Scenario: Local development loads .env before CreateBuilder
- **WHEN** the API application starts up in local development (not in docker)
- **THEN** `LoadEnvFile()` is called before `WebApplication.CreateBuilder()`
- **AND** environment variables from `.env` are set in the process environment
- **AND** conflicting user-secrets (`AuthKey__AbuseCh`, `PhishTank__ApiKey`) are removed
