## Purpose

Defines a production-like full-stack topology with deterministic startup, least exposure, durable state, bounded health behavior, and reproducible release versions.

## ADDED Requirements

### Requirement: Health-gated full-stack topology
The production topology SHALL include frontend, API, Worker, a one-shot migrator, SQL Server, and RabbitMQ. SQL Server and RabbitMQ SHALL become healthy before migration; migration SHALL complete successfully before API and Worker start; frontend SHALL start against the internal API. Migration failure SHALL prevent application hosts from starting.

#### Scenario: Clean stack starts
- **WHEN** an operator starts the production-like stack with valid configuration and empty volumes
- **THEN** migration completes once and every long-running service reaches its documented healthy state

#### Scenario: Migration fails
- **WHEN** the migrator exits unsuccessfully
- **THEN** API and Worker remain stopped and the failure is visible through Compose status and logs

### Requirement: Least-needed network exposure
Only the frontend SHALL bind a non-loopback host port by default. SQL, AMQP, RabbitMQ management, API, and Worker SHALL remain on internal networks. Optional diagnostic exposure SHALL require an explicit profile and bind to loopback.

#### Scenario: Production configuration is rendered
- **WHEN** the effective Compose configuration is inspected without diagnostic profiles
- **THEN** only the intended frontend port is publicly bound

### Requirement: Persistence and lifecycle policy
SQL and RabbitMQ SHALL use named data volumes. API and Worker logs SHALL be available through container logs and documented file volumes. Long-running services SHALL define restart policy, bounded health checks, and graceful-stop periods; the one-shot migrator SHALL not restart indefinitely. Minimum CPU, memory, and disk guidance SHALL be documented.

#### Scenario: Dependencies restart
- **WHEN** SQL Server or RabbitMQ is restarted without deleting volumes
- **THEN** durable state remains and API/Worker recover according to their documented readiness and reconnect behavior

### Requirement: Least-privilege configuration injection
One operator-owned `.env` SHALL remain the configuration source, but it SHALL NOT be mounted into containers and each service SHALL receive only its required keys. Frontend SHALL receive no backend secrets. Missing or placeholder required credentials SHALL fail preflight or startup without logging their values.

#### Scenario: Service environments are inspected
- **WHEN** an operator inspects frontend and Worker configuration
- **THEN** frontend has no backend secrets and Worker has no JWT signing or admin-seed credential unless its documented responsibility requires it

### Requirement: Versioned release images
Release images SHALL use an explicit release version and record immutable image digests and source revision. Rollback and reproduction SHALL NOT depend on a mutable `latest` tag.

#### Scenario: Release configuration is retained
- **WHEN** an operator opens a saved release record
- **THEN** the exact source revision and image digest for every service can be identified
