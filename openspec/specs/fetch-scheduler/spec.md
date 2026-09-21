# Fetch Scheduler Specification

## Purpose

Runs the worker-side scheduler that polls enabled, due providers and publishes scheduled FetchJobs, advancing `nextFetchTime` from each provider's configured interval.

## Requirements

### Requirement: Scheduled tick publishes due jobs
The system SHALL periodically evaluate providers that are enabled with a `nextFetchTime` at or before the current UTC time and SHALL publish a scheduled `FetchJob` for each due provider, creating a Queued history row with a `scheduled` trigger.

#### Scenario: Due provider tick
- **WHEN** a scheduler tick finds an enabled provider whose `nextFetchTime` is in the past
- **THEN** a scheduled FetchJob is published with a new logical jobId and a Queued history row

#### Scenario: Not-due or disabled provider skipped
- **WHEN** a scheduler tick finds a provider that is disabled or whose `nextFetchTime` is in the future
- **THEN** no job is published for that provider

#### Scenario: Duplicate ticks do not double-publish
- **WHEN** two scheduler ticks overlap for the same due provider
- **THEN** only one job is published (guards reserve the fetch atomically)

### Requirement: nextFetchTime advancement
After publishing a scheduled job, the system SHALL advance the provider's `nextFetchTime` by its configured interval. A newly seeded provider with a NULL `nextFetchTime` SHALL NOT be scheduled until its first successful manual fetch initializes it.

#### Scenario: Interval advancement
- **WHEN** a scheduled job is published for a provider with a 5-minute interval
- **THEN** `nextFetchTime` becomes the publish time plus 5 minutes

#### Scenario: Never-run provider not scheduled
- **WHEN** a provider has a NULL `nextFetchTime`
- **THEN** the scheduler skips it regardless of elapsed time

#### Scenario: First successful manual fetch initializes schedule
- **WHEN** the first successful manual fetch completes for a never-run provider
- **THEN** `nextFetchTime` is set to completion time plus the configured interval

### Requirement: Cooldown-guarded scheduled runs
When a scheduled run is blocked by a provider cooldown, the system SHALL skip the run and rely on the next tick after the cooldown rather than retrying immediately.

#### Scenario: Cooldown block defers the run
- **WHEN** a scheduled run is due but the provider/mode is still on cooldown
- **THEN** no job is published and the provider is re-evaluated on a later tick without a retry storm

### Requirement: Failed scheduled run cadence
A failed scheduled run SHALL NOT trigger a fast retry; the provider SHALL wait for the next scheduled time or a manual trigger.

#### Scenario: Failure does not reschedule early
- **WHEN** a scheduled fetch fails
- **THEN** the provider is not re-published until the next due time or a manual trigger

### Requirement: Runtime interval changes recompute schedule
After a provider has completed at least one successful fetch, changing its interval SHALL recompute `nextFetchTime` as that successful completion time plus the new interval. If the recomputed time is at or before now, the provider SHALL be eligible on the next scheduler tick. Disabling a provider SHALL prevent publication; re-enabling it SHALL use the recomputed time. A never-run provider SHALL retain NULL `nextFetchTime`.

#### Scenario: Shortened interval becomes due
- **WHEN** an Admin shortens an already-run provider's interval and the recomputed time is in the past
- **THEN** the scheduler may reserve it on the next tick subject to normal guards

#### Scenario: Never-run provider remains unscheduled after settings change
- **WHEN** an Admin changes or re-enables a provider that has never completed a successful fetch
- **THEN** `nextFetchTime` remains NULL until a successful manual fetch
