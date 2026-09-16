## Purpose

Gives administrators an accurate, bounded view of grouped database counts and structured application logs through safe frontend presentation.

## ADDED Requirements

### Requirement: Exact database summary
`/admin/summary` SHALL display the E10 `total`, `ipv4`, `ipv6`, `domain`, and `url` values without client-side recomputation. It SHALL show the time of the latest successful retrieval and provide explicit loading, refresh, stale, and failure states.

#### Scenario: Summary refresh succeeds
- **WHEN** a refreshed response contains changed counts
- **THEN** the displayed values and successful-retrieval time update from that response

#### Scenario: Summary refresh fails
- **WHEN** refresh fails after a summary was loaded
- **THEN** the prior values remain visible and are clearly marked stale with a retry action

### Requirement: Bounded log controls
`/admin/logs` SHALL provide an optional supported-level filter and an integer count control defaulting to 100 and constrained to 1 through 500. Invalid values SHALL produce field-level feedback and SHALL NOT issue a request. The frontend SHALL NOT accept or construct a log file path.

#### Scenario: Count exceeds the maximum
- **WHEN** an Admin enters a count of 501
- **THEN** validation explains the maximum and no log request is sent

### Requirement: Safe structured log presentation
The log viewer SHALL display newest-first timestamp, level, message, and optional exception fields returned by E10. It SHALL support explicit refresh and distinguish loading, empty, stale, and failure states. Every returned string SHALL be rendered as plain text.

#### Scenario: Arbitrary log content is returned
- **WHEN** a message or exception contains HTML-like content
- **THEN** the content appears literally and cannot execute or create markup

#### Scenario: Log refresh fails
- **WHEN** refreshing previously loaded logs fails
- **THEN** prior entries remain visible with a stale indicator and retry control
