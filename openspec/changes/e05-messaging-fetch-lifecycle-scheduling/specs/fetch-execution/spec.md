## Purpose

Defines how the worker executes a delivered FetchJob: one-at-a-time consumption, rechecking persisted guards, reusing one logical history row across retries and redelivery, and classifying errors into retryable or terminal outcomes.

## ADDED Requirements

### Requirement: One logical history row per job
Every `FetchJob` SHALL resolve to one uniquely keyed history row by its `jobId`. Queued state SHALL be set by the publisher, Running at the first execution, and Success or Failed terminally. Retries and redeliveries SHALL reuse the same row and SHALL NOT insert additional history rows.

#### Scenario: Publisher-queued job transitions to running
- **WHEN** a FetchJob is consumed for the first time
- **THEN** the matching history row and the provider's `lastFetchStatus` transition to Running with the same `startedAt`

#### Scenario: Retry reuses the running row
- **WHEN** a transient failure causes MassTransit to retry the message
- **THEN** no new history row is created and the same running row is reused under the same `jobId`

#### Scenario: Redelivery reuses the row
- **WHEN** a message is redelivered after a crash mid-execution
- **THEN** the consumer resolves the same logical row and does not duplicate history

### Requirement: Counter semantics
On Success the system SHALL record `recordsAdded` as newly inserted IOC rows and `recordsUpdated` as upserts targeting existing rows, including unchanged incoming values. On a whole-batch rollback SHALL both counters be 0.

#### Scenario: Successful fetch records counters
- **WHEN** a fetch commits with some new rows and some updated rows
- **THEN** the history row records the exact added and updated counts

#### Scenario: Rolled-back fetch records zero counters
- **WHEN** a batch is rolled back due to an invalid record
- **THEN** the history row is Failed with `recordsAdded = 0` and `recordsUpdated = 0`

### Requirement: Transient retry classification
The system SHALL classify transient failures (network, timeout, 5xx, temporary database failures) as retryable and SHALL retry them after 10 seconds, 30 seconds, and 1 minute, moving the message to the default `_error` queue after the initial attempt and three retries fail, marking the history row Failed.

#### Scenario: Transient failure retried with delays
- **WHEN** a fetch fails transiently
- **THEN** MassTransit retries after 10 seconds, then 30 seconds, then 1 minute

#### Scenario: Exhausted retries dead-letter
- **WHEN** the initial attempt and all three retries fail transiently
- **THEN** the message moves to the `_error` queue and the history row is marked Failed with the terminal error

### Requirement: Non-retryable failure classification
The system SHALL NOT retry PhishTank 429/quota, invalid-record, authentication, or integration-conflict failures. These SHALL fail the fetch immediately with a Failed history row and a clear error detail.

#### Scenario: Quota response fails immediately
- **WHEN** a PhishTank fetch receives a 429/rate-limit response
- **THEN** the fetch fails immediately with one attempt, a Failed history row, and zero IOC writes

#### Scenario: Invalid record fails the whole batch
- **WHEN** any provider record fails validation
- **THEN** the whole batch rolls back, the history row is Failed with the first offending record and reason, and no retry occurs

#### Scenario: Integration conflict fails the fetch
- **WHEN** a record's new normalized identity already belongs to another provider row
- **THEN** the fetch fails without merging and without retry

### Requirement: Provider status reflects lifecycle
The system SHALL update the provider's `lastFetchStatus` to Running at execution start and to Success or Failed at completion, and SHALL set `lastFetchAt` on success.

#### Scenario: Provider status mirrors job state
- **WHEN** a fetch starts, succeeds, or fails
- **THEN** the provider's `lastFetchStatus` transitions Running → Success or Failed and `lastFetchAt` is set on success