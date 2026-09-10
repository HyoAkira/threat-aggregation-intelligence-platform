## Purpose

Resolves each incoming provider record to an existing or new IOC row using the two provider-scoped identities, confirms hash lookups against the normalized value, and rejects identity conflicts rather than merging rows.

## ADDED Requirements

### Requirement: Records resolve by endpoint identity
The pipeline SHALL resolve an incoming record by the logical endpoint identity `(providerId, iocType, iocValueHash, port)` so a re-fetched record updates its existing provider row instead of creating a duplicate.

#### Scenario: Existing endpoint identity returns the stored row
- **WHEN** an incoming record matches a stored row on provider, IOC type, value hash, and port
- **THEN** the pipeline updates that stored row

#### Scenario: New endpoint identity produces an insert
- **WHEN** an incoming record has no stored row for its provider, IOC type, value hash, and port
- **THEN** the pipeline inserts a new row

### Requirement: Records resolve by provider-native identity
The pipeline SHALL also resolve an incoming record by `(providerId, providerRecordId)` and treat the provider record ID as authoritative, so a re-fetched record whose normalized value changed still updates the same provider row.

#### Scenario: Provider record ID matches a different value
- **WHEN** an incoming record matches a stored row by provider and provider record ID but carries a different normalized value
- **THEN** the pipeline updates that row to the new normalized identity

#### Scenario: Provider record ID is absent
- **WHEN** an incoming record has no stored row for its provider and provider record ID
- **THEN** the pipeline inserts a new row

### Requirement: Hash lookup confirms the normalized value
When the pipeline resolves a row by value hash, it SHALL compare the stored normalized value to the incoming normalized value and treat the row as matching only when the values are equal, so a hash collision is never silently treated as a match.

#### Scenario: Hash match is confirmed by value equality
- **WHEN** a row found by hash lookup has a stored value equal to the incoming value
- **THEN** the pipeline treats it as the matching row

#### Scenario: Hash collision is not treated as a match
- **WHEN** a row found by hash lookup has a stored value different from the incoming value
- **THEN** the pipeline does not treat that row as the incoming record's match

### Requirement: Identity conflicts reject the fetch
When the resolved provider-native identity maps to a row whose normalized identity now belongs to a different provider row, the pipeline SHALL reject the entire fetch rather than merge or repurpose rows.

#### Scenario: Normalized identity belongs to another row
- **WHEN** an incoming record's new normalized identity already belongs to another provider row
- **THEN** the pipeline rejects the entire fetch and reports the conflict