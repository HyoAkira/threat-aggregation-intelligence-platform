# false-positive-state Specification

## Purpose

Defines the query-time calculation of a provider IOC row's false-positive state as a 180-day staleness rule using the shared UTC clock, without persisting the flag.

## Requirements

### Requirement: False-positive state is calculated from staleness
The system SHALL calculate a provider row as false positive when its `updatedAt` is older than exactly 180 days before the current UTC time, using the shared clock; the flag SHALL be computed at query time and never persisted.

#### Scenario: A row older than 180 days is false positive
- **WHEN** a provider row's `updatedAt` is more than 180 days before the current UTC time
- **THEN** the row's calculated `isFalsePositive` is true

#### Scenario: A row within 180 days is not false positive
- **WHEN** a provider row's `updatedAt` is 180 days or less before the current UTC time
- **THEN** the row's calculated `isFalsePositive` is false

#### Scenario: The boundary is exact
- **WHEN** a provider row's `updatedAt` is exactly 180 days before the current UTC time
- **THEN** the row's calculated `isFalsePositive` is false

### Requirement: Reappearance clears the false-positive state
A successful upsert SHALL refresh the row's `updatedAt`, so a reappearing indicator is no longer calculated as false positive.

#### Scenario: A reappearing row clears the flag
- **WHEN** a previously stale provider row is upserted with a refreshed `updatedAt`
- **THEN** the row's calculated `isFalsePositive` becomes false