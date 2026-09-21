# Admin IOC Summary Timeline Specification

## Purpose

Provides administrators with a bounded, timezone-aware view of IOC ingestion activity by exposing the persisted added and updated counters from completed provider fetches as a daily timeline in the user's local calendar.

## Requirements

### Requirement: Time-bounded IOC activity contract

`GET /api/admin/summary/timeline` SHALL require the Admin role and accept `from` and `to` as ISO-8601 UTC instants plus a valid browser timezone identifier. The request range SHALL be interpreted as the half-open UTC interval `[from,to)`, while completed fetches SHALL be assigned to local calendar days in the requested timezone. The response SHALL contain the normalized UTC range, the requested timezone, a daily bucket identifier, a chronologically ascending `points` array, and whether at least one successful fetch contributed data in the range. Each point SHALL contain its local calendar date, its canonical UTC bucket start instant, and non-negative `recordsAdded` and `recordsUpdated` values.

#### Scenario: Timeline request succeeds
- **WHEN** an Admin requests a valid UTC interval and browser timezone
- **THEN** the API returns the normalized UTC range, timezone, daily points in ascending local-date order, and non-negative added and updated counters

#### Scenario: Non-admin requests the timeline
- **WHEN** a signed-in non-admin calls the timeline endpoint
- **THEN** the API denies the request using the existing Admin authorization behavior and does not return timeline data

### Requirement: Explicit range validation

The timeline endpoint SHALL reject missing or invalid UTC instants, an invalid timezone identifier, a range whose start is not earlier than its end, and ranges covering more than 31 local calendar days with a 400 ProblemDetails response. It SHALL not execute an unbounded history query for an invalid request.

#### Scenario: Custom range exceeds the maximum
- **WHEN** an Admin requests more than 31 local calendar days
- **THEN** the API returns 400 ProblemDetails and does not return partial timeline data

#### Scenario: Timezone or instant syntax is invalid
- **WHEN** an Admin supplies a non-ISO instant, an invalid timezone identifier, or a `from` that is not earlier than `to`
- **THEN** the API returns 400 ProblemDetails identifying the invalid range or timezone

### Requirement: Daily bucket completeness

For every local calendar date touched by the requested `[from,to)` interval, the response SHALL include exactly one point. Dates without contributing fetches SHALL contain zero values. The response SHALL identify a range with no successful fetches separately from a successful fetch that contributed zero added and updated records.

#### Scenario: There is a gap between fetches
- **WHEN** successful fetches occur on the first and third local days of a requested range but not on the second
- **THEN** the response includes the second day with zero added and zero updated values

#### Scenario: No successful fetch exists
- **WHEN** the requested range contains no successful fetch history
- **THEN** every daily point is zero and the response marks that no successful fetch contributed data

#### Scenario: UTC completion crosses a local-day boundary
- **WHEN** a successful fetch completes near midnight UTC and its completion instant belongs to a different local calendar day in the requested timezone
- **THEN** its counters are included in the point for that user's local calendar day
