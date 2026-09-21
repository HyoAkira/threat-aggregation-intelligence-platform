## Purpose

Provides rehearsable, non-destructive-by-default procedures for data recovery, release rollback, broker reconciliation, diagnosis, and secret rotation.

## ADDED Requirements

### Requirement: Verified SQL backup
The backup procedure SHALL create a timestamped SQL Server full copy-only backup with checksum in a dedicated backup location, run restore verification, and record release and schema metadata. `.env` and rendered release configuration SHALL be backed up separately with restrictive permissions; commands and output SHALL NOT disclose secrets.

#### Scenario: Pre-release backup completes
- **WHEN** an operator runs the documented backup procedure
- **THEN** a verified backup and matching release/schema record exist outside the database data volume

### Requirement: Disposable restore drill
The restore drill SHALL restore into a disposable Compose project and non-production database name, run database consistency/schema checks, and perform authenticated application smoke checks. It SHALL never overwrite production implicitly; a production restore SHALL require quiescing writers and explicit target confirmation.

#### Scenario: Restore is rehearsed
- **WHEN** the documented drill runs against a current backup
- **THEN** restored data and application readiness are proven without changing the source deployment

### Requirement: RabbitMQ recovery boundary
RabbitMQ volumes SHALL be documented as restart durability rather than message backup. The database SHALL remain authoritative for logical fetch history. Broker loss MAY lose or duplicate queued/in-flight delivery; recovery SHALL recreate topology and reconcile Queued/Running histories before retriggering through E05's idempotent `jobId` behavior.

#### Scenario: Broker data is lost
- **WHEN** RabbitMQ is recreated without its data volume
- **THEN** topology is restored and operators reconcile persisted logical jobs before issuing replacement fetches

### Requirement: Versioned application rollback
Rollback SHALL use retained previous image digests and saved configuration. Publishers and Worker SHALL be quiesced before evaluating schema compatibility. Compatible rollback SHALL preserve data volumes; incompatible rollback SHALL use the verified pre-release database backup. `docker compose down -v` SHALL NOT be part of rollback.

#### Scenario: Compatible application rollback
- **WHEN** the previous release supports the current schema
- **THEN** previous images start against preserved volumes and health plus authenticated smoke checks determine success

#### Scenario: Incompatible application rollback
- **WHEN** the previous release cannot use the current schema
- **THEN** the database is restored through the explicit backup path before previous images are accepted

### Requirement: Operator incident runbook
The runbook SHALL provide observations, safe actions, verification, and escalation for frontend/API/Worker health, SQL/RabbitMQ failure, provider authentication/network/429/parse/validation errors, cooldown 409, disabled providers, stuck Queued/Running jobs, `_error` inspection and replay, correlation/log lookup, disk pressure, and secret rotation. Replay SHALL preserve the original `jobId`, occur only after cause and terminal state are reviewed, and rely on consume-time guards.

#### Scenario: Unfamiliar operator diagnoses a rehearsed incident
- **WHEN** an operator follows any runbook scenario without source-code knowledge
- **THEN** they can identify the state, perform only bounded safe actions, verify recovery, and know when to escalate

#### Scenario: Secret is rotated
- **WHEN** a backend secret changes in the operator-owned `.env`
- **THEN** only affected services are recreated, health and authentication are verified, and secret values never appear in commands captured as evidence or logs
