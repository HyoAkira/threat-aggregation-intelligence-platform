## Purpose

Configures DotNetEnv to reliably load environment variables from .env files at application startup, replacing the current unreliable .NET configuration approach. Ensures all API keys and database credentials from .env are available at runtime. Application will fail to start if .env file is missing or keys are invalid.

## ADDED Requirements

### Requirement: .env file is loaded at application startup

The system SHALL use DotNetEnv to load the `.env` file during application startup, making all environment variables defined in `.env` available via `Environment.GetEnvironmentVariable`.

#### Scenario: DotNetEnv initializes and .env keys are accessible
- **WHEN** the API application starts up
- **THEN** `DotEnv.Load()` is called before `BuildWebApplication`
- **AND** `AuthKey__AbuseCh` resolves to the value from `.env`
- **AND** `PhishTank__ApiKey` resolves to the value from `.env`
- **AND** `SA_PASSWORD` resolves to the value from `.env`

### Requirement: Database schema accommodates API response data

The system SHALL ensure that database columns can accommodate data from ThreatFox API responses. The `Reference` column in `providers.ThreatFox` table must support strings up to 500 characters.

#### Scenario: Database schema validation before insertion
- **WHEN** inserting ThreatFox API data into the database
- **THEN** system validates column sizes match expected data formats
- **AND** if column size is insufficient, applies migration to increase size to 500 characters minimum
- **AND** insertion proceeds without truncation errors