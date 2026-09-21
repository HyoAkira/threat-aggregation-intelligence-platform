## MODIFIED Requirements

### Requirement: Provider seed defaults
The system SHALL seed exactly two providers with deterministic identities and defaults: ThreatFox with id 1, base URL `https://threatfox-api.abuse.ch`, enabled, a 5-minute interval, default mode `recent`, supported modes `recent` and `full`, a 5-minute recent cooldown, a 60-minute full cooldown, and a 5-minute minimum interval; PhishTank with id 2, base URL `https://data.phishtank.com`, enabled, an 8-hour interval, default mode `full`, supported mode `full`, no cooldown, and a 480-minute minimum interval. Both SHALL have a NULL `nextFetchTime` on first seed.

#### Scenario: Fresh database receives the two provider rows
- **WHEN** seeding runs against an empty database
- **THEN** exactly two provider rows exist with the documented ids, names, base URLs, enabled state, intervals, configuration, and NULL `nextFetchTime`

### Requirement: Provider seeding is idempotent
Re-running seeding on a database that already contains the provider rows SHALL NOT create duplicates or change the seeded ids and defaults.

#### Scenario: Restarted environment does not duplicate providers
- **WHEN** seeding runs again on a database that already has the two providers
- **THEN** no new provider rows are added and the existing rows keep their ids and defaults

#### Scenario: Operator changes are not overwritten by reseed
- **WHEN** an operator has modified a provider's enabled state, interval, or capability configuration and seeding runs again
- **THEN** seeding does not reset those runtime-editable values
