## ADDED Requirements

### Requirement: Provider configuration columns
The `providers` table SHALL persist provider fetch configuration: `defaultMode` as a non-null `tinyint` ordinal, `supportedModes` as a non-null integer mode bitmask, `recentCooldownMinutes` and `fullCooldownMinutes` as nullable integers, and `minimumIntervalMinutes` as a non-null integer.

#### Scenario: Generated schema carries provider configuration
- **WHEN** the generated schema is inspected
- **THEN** the `providers` table has the documented configuration columns with the documented types and nullability

### Requirement: Provider configuration migration
A migration SHALL add the provider configuration columns to an existing `providers` table without data loss and SHALL backfill the seeded ThreatFox and PhishTank rows with their documented capability, cooldown, and minimum-interval defaults.

#### Scenario: Existing database is upgraded without data loss
- **WHEN** the migration set is applied to a database that already contains providers
- **THEN** the configuration columns are added and no existing provider row is lost

#### Scenario: Seeded rows are backfilled
- **WHEN** the migration runs against a database that already contains the seeded providers
- **THEN** ThreatFox and PhishTank receive their documented default mode, supported modes, cooldowns, and minimum intervals
