## MODIFIED Requirements

### Requirement: Application connects to SQL Server via DefaultConnection

The system SHALL configure EF Core to use the SQL Server provider with the connection string named `DefaultConnection` from configuration. The connection string SHALL be assembled from the configured server, database, user, and options, with the password injected from the `SA_PASSWORD` environment variable as defined by the `persistence/database-credentials` capability.

#### Scenario: DbContext is registered and resolves at startup
- **WHEN** the API application starts up
- **THEN** `AddInfrastructure` registers `ThreatIntelDbContext` with the SQL Server provider
- **AND** the connection string resolves from `ConnectionStrings:DefaultConnection`
- **AND** the password segment is sourced from the `SA_PASSWORD` environment variable