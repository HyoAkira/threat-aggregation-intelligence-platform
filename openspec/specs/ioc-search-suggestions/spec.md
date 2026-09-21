# ioc-search-suggestions Specification

## Purpose

Provides bounded, anonymous IOC-value suggestions that help analysts complete the active line in the bulk search editor without replacing free-form IOC entry or changing search matching semantics.

## Requirements

### Requirement: Prefix-based IOC suggestions
`GET /api/iocs/suggestions` SHALL accept a non-empty trimmed `q` prefix and an optional `limit` that defaults to 8 and is restricted to 1–20. The endpoint SHALL return a JSON array of unique normalized IOC values whose value begins with the case-insensitive prefix, with each item containing the value and lowercase IOC type token. A missing, blank, or overlong prefix, or an invalid limit, SHALL return the shared 400 ProblemDetails response.

#### Scenario: Matching values are returned by prefix
- **WHEN** the client requests `q=evil.example` and stored normalized values begin with that prefix
- **THEN** the response contains unique matching values with their IOC type tokens

#### Scenario: Prefix matching ignores case and surrounding whitespace
- **WHEN** the client requests `q=%20EVIL.EXAMPLE%20`
- **THEN** the endpoint trims the prefix and matches normalized values case-insensitively

#### Scenario: No values match
- **WHEN** the prefix does not match any stored normalized IOC value
- **THEN** the endpoint returns HTTP 200 with an empty array

### Requirement: Stable bounded suggestion ordering
The suggestions SHALL contain at most `limit` items and SHALL be ordered deterministically by normalized IOC value and then IOC type token. The endpoint SHALL not return provider-row duplicates for the same `(iocType, normalized iocValue)` identity.

#### Scenario: Duplicate provider rows produce one suggestion
- **WHEN** multiple providers store the same normalized IOC identity
- **THEN** the response contains that value/type pair once

#### Scenario: Limit is enforced
- **WHEN** the client requests `limit=3` and more than three values match
- **THEN** the response contains exactly the first three values in the deterministic order

### Requirement: Suggestions remain advisory
The endpoint SHALL not validate that a suggested value will match the current tag/provider filters, SHALL not submit a search, and SHALL not prevent the client from entering a value that is absent from the suggestions.

#### Scenario: A suggestion does not perform a search
- **WHEN** the client requests suggestions while the user is editing a line
- **THEN** the endpoint returns catalog values only and no search result or search side effect occurs

#### Scenario: Free-form value remains valid input
- **WHEN** the user submits a syntactically valid IOC value that was not returned by suggestions
- **THEN** bulk search evaluates it normally rather than rejecting it because it was absent from the catalog
