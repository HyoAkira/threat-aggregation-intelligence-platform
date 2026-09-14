# Manual Fetch Triggers Specification

## Purpose

Exposes the admin endpoints that validate provider capabilities, apply trigger guards, persist a Queued history row, and publish a `FetchJob`, returning 202 only after the broker confirms publication.

## Requirements

### Requirement: Manual fetch with default mode
`POST /api/admin/providers/{id}/fetch` SHALL trigger the provider's persisted default mode (seeded: ThreatFox `recent`, PhishTank `full`). On success it SHALL persist a Queued `fetchHistory` row with a new logical `jobId`, publish a `FetchJob` with publisher confirmation, and return **202 Accepted** with `{jobId, status: "queued"}`. A guarded or unsupported request SHALL be rejected before any publish.

#### Scenario: Valid default-mode trigger
- **WHEN** an Admin requests a fetch for an enabled provider that is not on cooldown
- **THEN** a Queued history row with a new jobId is persisted for the provider's default mode, a FetchJob is confirmed to the broker, and 202 with the jobId is returned

#### Scenario: Full-only provider default trigger
- **WHEN** an Admin requests a fetch for PhishTank, whose persisted default mode is `full`
- **THEN** the queued job uses `full` rather than `recent`

#### Scenario: Guarded trigger returns 409
- **WHEN** an Admin requests a fetch while the provider is disabled, already queued/running, or on cooldown
- **THEN** the request returns 409 and no FetchJob is published

#### Scenario: Unknown provider returns 404
- **WHEN** an Admin requests a fetch for a provider that does not exist
- **THEN** the request returns 404 and nothing is published

### Requirement: Manual full-mode fetch
`POST /api/admin/providers/{id}/fetch/full` SHALL trigger a manual full fetch for a provider whose persisted supported modes include `full`. It SHALL return **400** for a provider that does not support `full`, **409** when the provider is already queued/running or its full cooldown has not elapsed, and **202** with `{jobId, status: "queued"}` after a confirmed publish otherwise.

#### Scenario: Full fetch accepted for ThreatFox
- **WHEN** an Admin requests full mode for a provider whose supported modes include `full` and that is outside its full cooldown
- **THEN** the request returns 202 with a jobId after a confirmed publish

#### Scenario: Full mode unsupported returns 400
- **WHEN** an Admin calls the `/fetch/full` shortcut for a provider whose supported modes exclude `full`
- **THEN** the request returns 400 and nothing is published

### Requirement: Publish failure surfaces as failure
When the broker does not confirm a manually triggered publish, the system SHALL mark the reserved history row Failed with the publish error and SHALL NOT return 202.

#### Scenario: Unconfirmed publish marks history failed
- **WHEN** the broker fails to confirm a manually triggered publish
- **THEN** the history row is marked Failed with the error detail and the response indicates failure rather than 202
