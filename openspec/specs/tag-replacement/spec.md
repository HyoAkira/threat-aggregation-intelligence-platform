# tag-replacement Specification

## Purpose

Defines how the pipeline normalizes and persists tag names and replaces a provider row's tag associations by scalar tag ID, so tags are shared, deduplicated, and reused safely across the chunks of a single fetch.

## Requirements

### Requirement: Tag names are normalized and deduplicated
The pipeline SHALL trim, lowercase, and deduplicate incoming tag names so each distinct normalized tag name is stored once.

#### Scenario: Tags are trimmed, lowercased, and deduplicated
- **WHEN** a record carries tags such as `" BotNet "`, `"botnet"`, and `"BOTNET"`
- **THEN** a single normalized tag `botnet` is used for the record's association

### Requirement: Tag names are cached by ID across chunks
The pipeline SHALL maintain a scalar dictionary of normalized tag names to tag IDs across the whole fetch, so a tag first created in one chunk is reused without error by later chunks.

#### Scenario: Tag created in an early chunk is reused later
- **WHEN** a tag name is first inserted in one chunk and appears again in a later chunk
- **THEN** the later chunk reuses the same tag ID without creating a duplicate or erroring

#### Scenario: New tag names are inserted once
- **WHEN** a normalized tag name is not yet present in the dictionary
- **THEN** the pipeline inserts it once, records its ID, and uses that ID for associations

### Requirement: Tag associations reference scalar IDs
IOC-tag rows SHALL reference tag IDs by scalar value rather than tracked tag entities, so clearing the change tracker between chunks cannot recreate or duplicate tags.

#### Scenario: Associations survive change-tracker clearing
- **WHEN** the change tracker is cleared after a chunk and later chunks add associations for the same tag
- **THEN** the associations reference the already-stored tag ID and no duplicate tag row is created

### Requirement: Removed tags disappear
When a provider row's tag set changes, the pipeline SHALL remove associations for tags no longer present while leaving the shared tag row itself intact.

#### Scenario: A removed provider tag is disassociated
- **WHEN** an upserted row no longer carries a tag it previously had
- **THEN** that row's association is removed and the tag row is not deleted

### Requirement: Tags retain source attribution
The pipeline SHALL preserve the association between each provider row and its own tags, so grouped query responses can attribute each tag to the source that supplied it.

#### Scenario: Shared tag stays attributed per source
- **WHEN** two provider rows share the same normalized tag
- **THEN** each row keeps its own association to that tag