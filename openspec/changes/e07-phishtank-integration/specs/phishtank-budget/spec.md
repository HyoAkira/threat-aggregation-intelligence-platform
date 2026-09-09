## Purpose

Defines the PhishTank-specific rolling download-budget exposure and 429 quota behavior that complement the general trigger guards, so the UI/API can report budget usage and quota responses fail the fetch immediately without retry.

## ADDED Requirements

### Requirement: Rolling budget exposure
The system SHALL report `fetchesPast24h` for the PhishTank provider: the number of logical fetch starts (scheduled and manual combined) recorded in the rolling 24 hours.

#### Scenario: Count reflects recent starts
- **WHEN** the provider dashboard queries the PhishTank provider
- **THEN** `fetchesPast24h` equals the number of logical fetch starts in the rolling 24 hours

#### Scenario: Window boundary is exact
- **WHEN** a fetch start is exactly at the edge of the rolling 24-hour window
- **THEN** the count includes only starts strictly inside the window

### Requirement: Budget semantics for the adapter
PhishTank fetches SHALL be limited to fewer than three logical starts in any rolling 24-hour window, with scheduled and manual triggers sharing the budget and internal retries of one job consuming no additional budget. A blocked manual trigger SHALL be rejected with 409.

#### Scenario: Fourth start in the window blocked
- **WHEN** three PhishTank logical starts are already recorded in the rolling 24 hours
- **THEN** a further manual trigger is rejected with 409 and no job is published

#### Scenario: Budget frees when the oldest start leaves the window
- **WHEN** the oldest in-window fetch falls outside the rolling 24 hours
- **THEN** a new trigger is allowed

#### Scenario: Retries do not consume budget
- **WHEN** a PhishTank job is retried after a transient failure
- **THEN** `fetchesPast24h` and the budget count do not increase

### Requirement: PhishTank 429 immediate failure
A PhishTank HTTP 429/rate-limit response SHALL fail the fetch immediately with a single attempt, a Failed history row with a clear operational reason, and zero IOC writes, without retry.

#### Scenario: Simulated 429 results in one attempt
- **WHEN** the upstream responds with 429 during a fetch
- **THEN** the fetch performs exactly one attempt, the history row is Failed with the quota reason, and no IOC rows are written