# ioc-tag-catalog Specification

## Purpose

Defines the public catalog used to discover normalized IOC tags before applying the existing browse or search tag filters.

## ADDED Requirements

### Requirement: Bounded tag catalog endpoint

`GET /api/iocs/tags` SHALL be anonymous and SHALL require an integer `limit` query parameter from 1 through 100. It SHALL return a JSON array of objects with `name` and `usageCount`, where `usageCount` is the number of persisted `iocTags` association rows for that tag. The endpoint SHALL read existing tag data only and SHALL NOT create, mutate, or delete tags.

#### Scenario: Catalog returns usage counts

- **WHEN** the request is `GET /api/iocs/tags?limit=10` and tags have persisted associations
- **THEN** the response is 200 and each item contains the normalized tag name and its association-row count

#### Scenario: Limit is required and bounded

- **WHEN** the request omits `limit`, sends a non-integer, or sends a value below 1 or above 100
- **THEN** the response is 400 with the shared invalid-input problem shape

### Requirement: Usage ordering is deterministic

Results SHALL be ordered by `usageCount` descending and then `name` ascending using ordinal ordering. Tags with zero associations SHALL be eligible and SHALL sort after tags with associations.

#### Scenario: Equal counts use name tie-break

- **WHEN** two tags have the same association count
- **THEN** the lexicographically lower normalized name appears first

### Requirement: Prefix autocomplete filtering

When `q` is supplied, the server SHALL trim and lowercase it using the same normalization convention as stored tag names and SHALL return only names beginning with that normalized prefix. A blank or whitespace-only `q` SHALL behave as if `q` were omitted. No result SHALL be returned for tags outside the prefix.

#### Scenario: Query is normalized

- **WHEN** the request is `GET /api/iocs/tags?q=  BOT  &limit=10`
- **THEN** it matches the normalized `bot...` tag names

#### Scenario: No prefix match is empty

- **WHEN** the normalized prefix matches no tag
- **THEN** the response is 200 with an empty JSON array

### Requirement: Frontend tag filters expose catalog suggestions

The browse and search tag filter controls SHALL request bounded catalog suggestions as the user types, display matching tag names as autocomplete choices, and keep the selected/submitted value as the existing trimmed tag query parameter. A catalog request failure SHALL not prevent manually entering a tag or submitting the existing filter.

#### Scenario: User selects a suggested tag

- **WHEN** a user types a tag prefix and selects a catalog suggestion
- **THEN** the corresponding browse or search filter contains that normalized tag value and uses the existing filtering request

#### Scenario: Manual tag entry remains available

- **WHEN** the catalog request fails or has no suggestions
- **THEN** the user can still enter and submit a tag value manually
