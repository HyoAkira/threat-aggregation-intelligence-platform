## Purpose

Configures DotNetEnv to reliably load environment variables from .env files at application startup, replacing the current unreliable .NET configuration approach. Ensures all API keys and database credentials from .env are available at runtime.

## ADDED Requirements

### Requirement: .env file is loaded at application startup

The system SHALL use DotNetEnv to load the `.env` file during application startup, making all environment variables defined in `.env` available via `Environment.GetEnvironmentVariable`.

#### Scenario: DotNetEnv initializes and .env keys are accessible
- **WHEN** the API application starts up
- **THEN** `DotEnv.Load()` is called before `BuildWebApplication`
- **AND** `AuthKey__AbuseCh` resolves to the value from `.env`
- **AND** `PhishTank__ApiKey` resolves to the value from `.env`
- **AND** `SA_PASSWORD` resolves to the value from `.env`

### Requirement: Missing .env file produces warning, not crash

The system SHALL handle a missing `.env` file gracefully, logging a warning instead of failing to start, while still allowing environment variables to be provided through other means (e.g., system environment variables).

#### Scenario: .env file is optional - warning on missing
- **WHEN** the application starts without a `.env` file present
- **THEN** DotNetEnv logs a warning but startup continues
- **AND** environment variables from system scope remain accessible