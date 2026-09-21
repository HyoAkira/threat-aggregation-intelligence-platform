## Purpose

Provides a bounded API for submitting multiple IOC values together while preserving the existing single-value normalization, matching, filtering, grouping, and source-attributed response semantics for each submitted value.

## ADDED Requirements

### Requirement: Bounded bulk-search request
`POST /api/iocs/search/bulk` SHALL accept a JSON object containing a `queries` array, optional `tag` and `provider` filters, and optional shared `page` and `pageSize` values. After blank or whitespace-only entries are removed, the request SHALL contain at least one query and no more than 100 non-empty entries. Each non-empty query SHALL be trimmed and SHALL be no longer than 2048 characters. `page` SHALL default to 1 and `pageSize` SHALL default to 20; both SHALL use the existing inclusive bounds of page at least 1 and pageSize 1–100. A missing body, malformed JSON, missing `queries`, an empty effective list, an oversized list, an oversized query, or an invalid page value SHALL return the shared 400 ProblemDetails response without executing a search.

#### Scenario: A multiline list is accepted
- **WHEN** the request contains three non-empty query strings and optional tag/provider filters
- **THEN** the endpoint accepts the request and evaluates the three values using the supplied filters

#### Scenario: Blank lines are ignored
- **WHEN** the request contains empty and whitespace-only entries between valid query strings
- **THEN** blank entries do not produce result items or count toward the 100-query limit

#### Scenario: Too many values are rejected
- **WHEN** the request contains 101 non-empty query strings
- **THEN** the endpoint returns 400 with a detail identifying the maximum bulk-query count

### Requirement: Ordered per-query outcomes
For a valid bulk request, the endpoint SHALL return HTTP 200 with a response containing one item for every non-empty submitted query, in the same order as the effective input list. Each item SHALL include the trimmed original `input` and exactly one status: `matched`, `not_found`, or `invalid`. A `matched` item SHALL include one or more existing source-attributed IOC groups, plus the shared `page`, `pageSize`, and `totalCount` metadata for that query, and no error; a `not_found` item SHALL include an empty groups collection and a not-found code/detail; an `invalid` item SHALL include an empty groups collection and an invalid-input code/detail. An invalid or absent item SHALL NOT cause the whole request to fail when other items can be evaluated. Duplicate input lines SHALL each receive an outcome, while equivalent work MAY be shared internally.

#### Scenario: Mixed inputs return mixed statuses
- **WHEN** a request contains a stored IOC, a valid IOC with no stored match, and an unclassifiable string
- **THEN** the response is 200 and contains, in input order, one `matched`, one `not_found`, and one `invalid` item

#### Scenario: Duplicate lines retain input order
- **WHEN** the same IOC value appears twice with another value between the copies
- **THEN** the response contains two corresponding outcomes in the same positions as the submitted lines

#### Scenario: A matched item uses the shared group contract
- **WHEN** one submitted value matches multiple provider rows or domain-related URL rows
- **THEN** its groups use the existing `IocGroupResult` shape, source attribution, aggregation, and deterministic ordering

### Requirement: Bulk matching and filters reuse exact-search semantics
Each non-empty query SHALL be classified and normalized with the same rules as `GET /api/iocs/search`. IP and URL values SHALL use exact normalized identity matching with the existing port behavior; domain values SHALL include exact domain, URL-host, and parent-domain matches. The optional tag and provider filters SHALL be applied to each query independently, and a valid query whose groups are eliminated by those filters SHALL be reported as `not_found`. Group-level aggregates SHALL be computed from the filtered sources returned for that query.

#### Scenario: Domain input returns related URL groups
- **WHEN** a submitted domain has stored URL rows whose `urlHost` or `parentDomain` matches
- **THEN** the item's `matched` groups include those URL groups using the same relation rules as exact search

#### Scenario: Filters apply independently to every input
- **WHEN** a bulk request includes a tag filter and only some submitted values have matching tagged sources
- **THEN** each value is evaluated with the filter and only the values with matching groups are `matched`

#### Scenario: Invalid input does not echo sensitive content
- **WHEN** a submitted value cannot be classified or normalized
- **THEN** its invalid detail identifies the validation reason without exposing secrets beyond the bounded submitted input context

### Requirement: Bulk result summary and deterministic execution bounds
The bulk response SHALL include `totalCount`, `matchedCount`, `notFoundCount`, and `invalidCount`, where the four counts describe the returned item statuses and sum to `totalCount`. The endpoint SHALL keep database work bounded by the request limit and SHALL NOT create an unbounded background task or request fan-out per submitted value. The endpoint SHALL remain anonymous-accessible under the same authorization policy as the existing public IOC query endpoints.

#### Scenario: Summary counts match item statuses
- **WHEN** a request produces two matches, one not-found item, and one invalid item
- **THEN** the response summary reports totalCount 4, matchedCount 2, notFoundCount 1, and invalidCount 1

#### Scenario: Anonymous bulk search is allowed
- **WHEN** an unauthenticated client submits a valid bounded bulk request
- **THEN** the endpoint returns the same search outcomes it would return to an authenticated user
