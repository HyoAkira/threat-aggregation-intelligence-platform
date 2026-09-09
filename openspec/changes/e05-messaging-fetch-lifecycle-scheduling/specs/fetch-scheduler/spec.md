## Purpose

Runs the worker-side scheduler that polls enabled, due providers and publishes scheduled FetchJobs, advancing `nextFetchTime` while respecting trigger guards and the PhishTank budget.

## ADDED Requirements

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

### Requirement: Budget-blocked scheduled runs
When the PhishTank budget window blocks a scheduled run, the system SHALL advance `nextFetchTime` past the blocking window instead of publishing.

#### Scenario: Budget exhausted pushes next fetch later
- **WHEN** a scheduled PhishTank run is due but the rolling 24-hour budget is exhausted
- **THEN** no job is published and `nextFetchTime` is pushed past the time the oldest in-window fetch leaves the window

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