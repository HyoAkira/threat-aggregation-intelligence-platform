# Admin Observability Specification

## Purpose

Gives administrators bounded, source-safe operational visibility through grouped counts, recent logs, and correlated fetch lifecycle events.

## Requirements

### Requirement: Grouped database summary
`GET /api/admin/summary` SHALL require the Admin role and return `{total,ipv4,ipv6,domain,url}` counts of distinct `(iocType,iocValue)` groups. Cross-provider duplicates and port variants SHALL count once in the matching type.

#### Scenario: Equivalent sources count once
- **WHEN** two providers and two ports store the same normalized IPv4 value
- **THEN** the summary adds one to `ipv4` and one to `total`

### Requirement: Safe bounded log tail
`GET /api/admin/logs` SHALL return newest-first structured log entries with `timestamp`, `level`, `message`, and optional `exception`. `count` SHALL default to 100 and be between 1 and 500; `level` SHALL use the supported log levels. The endpoint SHALL read only the configured rolling-log files, tolerate rotation and malformed partial lines, and SHALL NOT accept a file path from the caller. Returned entries SHALL redact credentials, credential-bearing URLs, connection strings, tokens, and provider keys.

#### Scenario: Rotation and malformed line are tolerated
- **WHEN** the active log has rotated and one candidate line is incomplete
- **THEN** valid recent entries are still returned and the malformed line does not fail the request

#### Scenario: Invalid log query is rejected
- **WHEN** `count` exceeds 500 or `level` is unknown
- **THEN** the API returns 400 ProblemDetails

### Requirement: Correlated fetch lifecycle events
The API and Worker SHALL emit structured events for reservation, guard rejection, execution start, retry, success, and terminal failure. Each event SHALL include correlation ID, `jobId`, provider ID, mode, trigger, and event name; completion events SHALL also include duration and counters. Events SHALL NOT contain credentials, credential-bearing URLs, full IOC values, or provider payloads.

#### Scenario: One fetch is traceable
- **WHEN** a fetch is accepted, retried, and succeeds
- **THEN** an operator can select all lifecycle events using its `jobId` and correlation ID without seeing secret or IOC payload data

### Requirement: Timeline uses persisted fetch-history counters

The Admin timeline SHALL use the persisted `recordsAdded` and `recordsUpdated` counters from successful fetch-history records as its source of truth. Each qualifying history record SHALL contribute its counters once to the local calendar date obtained by converting its UTC completion time into the requested browser timezone. Queued, running, failed, or unfinished records SHALL not contribute activity values, and reading the timeline SHALL not mutate IOC or fetch-history data.

#### Scenario: Successful provider runs are aggregated
- **WHEN** two successful provider fetches complete on the same requested local date with added and updated counters
- **THEN** that local-date point equals the sum of both records' counters

#### Scenario: UTC storage is converted to the user's local date
- **WHEN** a successful fetch completion is stored in UTC near a UTC midnight boundary
- **THEN** the timeline assigns its counters to the local calendar date produced by the requested timezone, rather than blindly using the UTC date

#### Scenario: Non-terminal or failed runs are excluded
- **WHEN** a range contains queued, running, failed, or unfinished fetch-history records
- **THEN** those records contribute zero to the timeline

#### Scenario: Existing summary semantics remain unchanged
- **WHEN** the timeline is queried after IOC data is ingested
- **THEN** `GET /api/admin/summary` continues to return distinct grouped IOC counts without using timeline counters for client-side recomputation
