## Purpose

Defines how the pipeline upserts a single provider IOC row so re-fetches update without duplicates: immutable fields are preserved, mutable metadata and timestamps are refreshed, and tag associations are replaced.

## ADDED Requirements

### Requirement: Immutable fields are preserved on upsert
When an existing provider row is upserted, the pipeline SHALL preserve the row's `createdAt` and its stored `firstSeenUtc` rather than replacing them with the incoming values.

#### Scenario: Re-fetch preserves creation and first-seen
- **WHEN** an existing provider row is upserted with new incoming values
- **THEN** the row's `createdAt` and stored `firstSeenUtc` remain unchanged

### Requirement: Mutable fields and timestamps are refreshed
When an existing provider row is upserted, the pipeline SHALL refresh `updatedAt` and replace provider metadata and the incoming `lastSeenUtc`, without applying any timestamp-regression guard.

#### Scenario: Re-fetch refreshes update and last-seen
- **WHEN** an existing provider row is upserted
- **THEN** the row's `updatedAt` is refreshed and its `lastSeenUtc` is replaced with the provider's incoming value

#### Scenario: New row initializes timestamps
- **WHEN** a new provider row is inserted
- **THEN** the row records `createdAt`, `updatedAt`, and the incoming `firstSeenUtc` and `lastSeenUtc`

### Requirement: Provider metadata is replaced
When an existing provider row is upserted, the pipeline SHALL replace the row's current provider-specific metadata with the incoming metadata, so stale values no longer persist.

#### Scenario: Re-fetch replaces metadata
- **WHEN** an existing provider row is upserted with new provider metadata
- **THEN** the stored metadata reflects only the incoming values

### Requirement: Tag associations are replaced per upsert
When an existing provider row is upserted, the pipeline SHALL replace the row's tag associations with the provider's current tag set, so removed tags disappear and retained tags stay attributed to their source.

#### Scenario: Re-fetch replaces tag associations
- **WHEN** an existing provider row is upserted with a changed tag set
- **THEN** the row's tag associations reflect only the incoming set

### Requirement: Upsert counts as an update
An upsert that targets an existing provider row SHALL count as one updated record even when the incoming values are identical, so that added plus updated counts equal the number of committed provider records.

#### Scenario: Identical re-fetch still counts as updated
- **WHEN** an existing provider row is upserted with identical values
- **THEN** it increments the updated count, not the added count