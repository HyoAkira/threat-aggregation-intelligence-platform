# Fetch Job Contract Specification

## Purpose

Defines the versioned `FetchJob` message that the API and the worker scheduler publish and the worker consumer executes, so publishers and consumers agree on one contract.

## Requirements

### Requirement: FetchJob contract fields
The `FetchJob` message SHALL carry a non-empty logical job identifier, a valid provider identifier, a defined trigger (`scheduled` or `manual`), a defined mode (`recent` or `full`), an attempt number, and a non-empty correlation identifier. The correlation identifier SHALL default to the job identifier when the publisher does not supply one. The job identifier SHALL be the `jobId` of a persisted `fetchHistory` row created by the publisher before publication.

#### Scenario: Valid FetchJob
- **WHEN** a publisher creates a FetchJob for a known provider with a non-empty job ID, a defined trigger and mode, and attempt 1
- **THEN** the message is serializable and accepted by the consumer

#### Scenario: Invalid FetchJob rejected
- **WHEN** a message is missing the job ID, names an unknown provider, uses an undefined trigger or mode, or carries a non-positive attempt
- **THEN** serialization or deserialization fails rather than producing an ambiguously interpreted job

#### Scenario: Correlation id defaults to the job
- **WHEN** a publisher creates a FetchJob without an explicit correlation identifier
- **THEN** the message carries the job identifier as its correlation identifier

### Requirement: Publisher and consumer share one contract
The API, the scheduler, and the worker SHALL serialize and deserialize `FetchJob` against a single shared contract so a message published by either publisher is consumed identically.

#### Scenario: Golden message round-trips
- **WHEN** a FetchJob is serialized and deserialized by both the API and worker contract tests
- **THEN** all fields, including the correlation identifier, match the shared golden message byte-for-byte

#### Scenario: Correlation id flows to the consumer
- **WHEN** a publisher sends a FetchJob with a correlation identifier
- **THEN** the delivered message carries the same identifier so the API and Worker lifecycle events for that job are selectable by one correlation id

#### Scenario: Attempt reflects job attempts
- **WHEN** the worker retries a transiently failed fetch
- **THEN** the delivered message still addresses the same logical job ID and the consumer knows it is a retry of that job rather than a new job
