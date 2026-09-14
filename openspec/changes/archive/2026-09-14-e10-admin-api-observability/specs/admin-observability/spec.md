## Purpose

Gives administrators bounded, source-safe operational visibility through grouped counts, recent logs, and correlated fetch lifecycle events.

## ADDED Requirements

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
