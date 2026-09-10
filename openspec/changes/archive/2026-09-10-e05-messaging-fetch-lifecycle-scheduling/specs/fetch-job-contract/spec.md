## Purpose

Defines the versioned `FetchJob` message that the API and the worker scheduler publish and the worker consumer executes, so publishers and consumers agree on one contract.

## ADDED Requirements

### Requirement: FetchJob contract fields
The `FetchJob` message SHALL carry a non-empty logical job identifier, a valid provider identifier, a defined trigger (`scheduled` or `manual`), a defined mode (`recent` or `full`), and an attempt number. The job identifier SHALL be the `jobId` of a persisted `fetchHistory` row created by the publisher before publication.

#### Scenario: Valid FetchJob
- **WHEN** a publisher creates a FetchJob for a known provider with a non-empty job ID, a defined trigger and mode, and attempt 1
- **THEN** the message is serializable and accepted by the consumer

#### Scenario: Invalid FetchJob rejected
- **WHEN** a message is missing the job ID, names an unknown provider, uses an undefined trigger or mode, or carries a non-positive attempt
- **THEN** serialization or deserialization fails rather than producing an ambiguously interpreted job

### Requirement: Publisher and consumer share one contract
The API, the scheduler, and the worker SHALL serialize and deserialize `FetchJob` against a single shared contract so a message published by either publisher is consumed identically.

#### Scenario: Golden message round-trips
- **WHEN** a FetchJob is serialized and deserialized by both the API and worker contract tests
- **THEN** all fields match the shared golden message byte-for-byte

#### Scenario: Attempt reflects job attempts
- **WHEN** the worker retries a transiently failed fetch
- **THEN** the delivered message still addresses the same logical job ID and the consumer knows it is a retry of that job rather than a new job