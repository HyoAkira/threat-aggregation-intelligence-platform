## Purpose

Allows administrators to inspect and atomically change non-secret runtime settings while preserving provider cadence and preventing partial updates.

## ADDED Requirements

### Requirement: Read non-secret settings
`GET /api/admin/settings` SHALL require the Admin role and return the global log level plus each provider's `id`, `name`, `isEnabled`, and `intervalMinutes`. It SHALL NOT return environment variables, connection strings, broker credentials, JWT secrets, admin credentials, or provider API keys.

#### Scenario: Settings response contains no secrets
- **WHEN** an Admin reads settings
- **THEN** only database-backed runtime settings and provider controls are returned

### Requirement: Atomic runtime settings update
`PUT /api/admin/settings` SHALL validate the complete request before writing. Provider intervals SHALL be positive and SHALL respect the documented scheduled minimums: ThreatFox recent at least 5 minutes and PhishTank full at least 480 minutes. Log level SHALL be one of `Verbose`, `Debug`, `Information`, `Warning`, `Error`, or `Fatal`. If any value is invalid, the API SHALL return 400 and persist none of the request.

#### Scenario: Valid update applies at runtime
- **WHEN** an Admin submits valid provider and log-level values
- **THEN** the API persists them and API/Worker behavior reflects them within 30 seconds without restart

#### Scenario: Invalid update is atomic
- **WHEN** one value in a multi-setting request is invalid
- **THEN** the API returns 400 and no setting or provider value changes

### Requirement: Schedule recomputation after provider changes
Enabling, disabling, shortening, or lengthening a provider interval SHALL be observed by the E05 scheduler within 30 seconds. Disabling SHALL prevent new jobs. Re-enabling a never-run provider SHALL leave `nextFetchTime` null; otherwise the next time SHALL be recomputed from the most recent successful completion and the new interval without violating provider cooldowns.

#### Scenario: Disable stops scheduling
- **WHEN** an Admin disables a provider
- **THEN** no new scheduled fetch is published after the runtime update is observed
