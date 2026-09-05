## Purpose

Defines the EF Core persistence infrastructure that connects the application
to the `ThreatIntel` database on the running MSSQL Server container: the
`DefaultConnection` string, the `ThreatIntelDbContext`, and EF migration
workflow (applied manually via `dotnet ef database update`).

## Requirements

### Requirement: Application connects to SQL Server via DefaultConnection

The system SHALL configure EF Core to use the SQL Server provider with the
connection string named `DefaultConnection` from configuration.

#### Scenario: DbContext is registered and resolves at startup
- **WHEN** the API application starts up
- **THEN** `AddInfrastructure` registers `ThreatIntelDbContext` with the SQL Server provider
- **AND** the connection string resolves from `ConnectionStrings:DefaultConnection`

### Requirement: Database schema is provisioned via EF migrations

The system SHALL provide an EF migration that creates the `ThreatIntel` database
schema (tables `threat.Provider`, `providers.ThreatFox`, `providers.PhishTank`,
unique indexes). Migrations are applied manually via
`dotnet ef database update` — the API does NOT auto-migrate at startup.

#### Scenario: Database is created via migration
- **WHEN** a developer runs `dotnet ef database update`
- **THEN** the `ThreatIntel` database and all tables are created
- **AND** subsequent runs are no-ops when already at the latest migration

### Requirement: Provider entity set is queryable

The system SHALL expose a `DbSet<Provider>` from the `ThreatIntelDbContext`
so that provider configuration can be read at runtime.

#### Scenario: Provider count is available via the context
- **WHEN** a caller opens `ThreatIntelDbContext`
- **THEN** `Providers` DbSet is accessible for querying
- **AND** provider rows seeded during migration are returned
