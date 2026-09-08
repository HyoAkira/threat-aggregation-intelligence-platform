## MODIFIED Requirements

### Requirement: Database schema is provisioned via EF migrations

The system SHALL provide an EF Core migration that removes the provider-specific raw tables and creates the common `providers`, `iocs`, `tags`, and `iocTags` tables with their required keys, relationships, indexes, and constraints. The migration SHALL intentionally discard existing development data and SHALL continue to be applied manually; the API SHALL NOT apply migrations during startup.

#### Scenario: Database is created via migration
- **WHEN** a developer applies all migrations to a fresh database
- **THEN** the common ingestion tables and seeded provider rows are created
- **AND** subsequent runs are no-ops when the database is current

#### Scenario: Common ingestion schema is created via migration
- **WHEN** a developer applies the new migration to a database containing the legacy raw schema
- **THEN** the legacy provider-specific tables and their data are removed
- **AND** the common ingestion tables, relationships, indexes, and constraints are created
- **AND** subsequent migration runs are no-ops when the database is current

#### Scenario: Migration is not applied automatically
- **WHEN** the API starts against a database that has not received the new migration
- **THEN** startup does not alter the database schema

### Requirement: Provider entity set is queryable

The system SHALL expose queryable entity sets for `Provider`, `Ioc`, `Tag`, and `IocTag` from `ThreatIntelDbContext`. It SHALL NOT expose entity sets for the removed provider-specific raw entities.

#### Scenario: Provider count is available via the context
- **WHEN** a caller queries providers through `ThreatIntelDbContext`
- **THEN** seeded ThreatFox and PhishTank provider rows are returned

#### Scenario: Common ingestion sets are available
- **WHEN** an ingestion service uses `ThreatIntelDbContext`
- **THEN** provider, IOC, tag, and IOC-tag sets are accessible
- **AND** ThreatFox and PhishTank raw entity sets are absent
