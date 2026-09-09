## Purpose

Establishes the two provider rows in the database deterministically and idempotently so the application starts against a known provider set with the decided defaults.

## Requirements

### Requirement: Provider seed defaults
The system SHALL seed exactly two providers with deterministic identities and defaults: ThreatFox with id 1, base URL `https://threatfox-api.abuse.ch`, enabled, and a 5-minute interval; PhishTank with id 2, base URL `https://data.phishtank.com`, enabled, and an 8-hour interval. Both SHALL have a NULL `nextFetchTime` on first seed.

#### Scenario: Fresh database receives the two provider rows
- **WHEN** seeding runs against an empty database
- **THEN** exactly two provider rows exist with the documented ids, names, base URLs, enabled state, intervals, and NULL `nextFetchTime`

### Requirement: Provider seeding is idempotent
Re-running seeding on a database that already contains the provider rows SHALL NOT create duplicates or change the seeded ids and defaults.

#### Scenario: Restarted environment does not duplicate providers
- **WHEN** seeding runs again on a database that already has the two providers
- **THEN** no new provider rows are added and the existing rows keep their ids and defaults

#### Scenario: Operator changes are not overwritten by reseed
- **WHEN** an operator has modified a provider's enabled state or interval and seeding runs again
- **THEN** seeding does not reset those runtime-editable values