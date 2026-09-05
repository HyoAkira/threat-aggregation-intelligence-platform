## Purpose

Defines how the SQL Server connection string is resolved at startup so database credentials are never committed to source control. The connection string shape lives in configuration; the password is supplied by the `SA_PASSWORD` environment variable at runtime.

## ADDED Requirements

### Requirement: Database password is not stored in source control

The system SHALL NOT contain the database password in any committed configuration file. The password SHALL be read from the `SA_PASSWORD` environment variable.

#### Scenario: appsettings.json contains no real database password
- **WHEN** `appsettings.json` is inspected
- **THEN** the `DefaultConnection` connection string contains a placeholder, not the literal database password

#### Scenario: Password resolves from environment variable
- **WHEN** `AddInfrastructure` configures the DbContext with `SA_PASSWORD` set in the environment
- **THEN** the final connection string contains the value of `SA_PASSWORD`
- **AND** the placeholder is not present in the final connection string

### Requirement: Missing password fails fast

The system SHALL resolve a missing `SA_PASSWORD` to an empty password so the connection attempt fails with a SQL Server authentication error rather than starting with an undefined connection string.

#### Scenario: SA_PASSWORD is not set
- **WHEN** the application starts without `SA_PASSWORD` in the environment
- **THEN** the connection string is built with an empty password
- **AND** the database health check reports the connection failure at startup

### Requirement: Connection string preserves structure from configuration

The connection string's server, database, user, and options SHALL continue to come from configuration (`DefaultConnection`), with only the password injected from the environment.

#### Scenario: Full connection string is assembled from config + env
- **WHEN** the DbContext is configured
- **THEN** server, database, user id, and options match the configured connection string
- **AND** the password segment equals the environment-provided `SA_PASSWORD`