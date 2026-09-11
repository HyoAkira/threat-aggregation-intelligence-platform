# ioc-relations Specification

## Purpose

Defines opt-in, bounded queries for an IOC's relations: other IOC groups that share its exact `urlHost` or its registrable `parentDomain`. Relation data is never inlined without limit; it is always returned through a paginated endpoint the caller explicitly requests.

## Requirements

### Requirement: Opt-in paginated relation queries
Relation data SHALL be available only through `GET /api/iocs/{id}/relations`, which the caller opts into by requesting it; the detail endpoint SHALL NOT embed an unbounded inline list of related URLs. The request SHALL require a `relation` parameter of `urlHost` or `parentDomain` and SHALL return the paginated list envelope of IOC groups whose stored `urlHost` equals the resolved group's `urlHost` (relation `urlHost`) or whose stored `parentDomain` equals the resolved group's `parentDomain` (relation `parentDomain`). The resolved group itself SHALL be excluded from the related items. Page and pageSize SHALL follow the shared envelope bounds (default 20, maximum 100).

#### Scenario: URL groups sharing an exact host are listed
- **WHEN** a client requests `relation=urlHost` for a URL group whose `urlHost` is `evil.example`
- **THEN** the response returns, paginated, every other URL group with `urlHost=evil.example`, ordered deterministically

#### Scenario: Groups sharing a parent domain are listed
- **WHEN** a client requests `relation=parentDomain` for a domain group whose `parentDomain` is `example.co.uk`
- **THEN** the response returns, paginated, the domain and URL groups whose stored `parentDomain` is `example.co.uk`

#### Scenario: No inline related URLs on detail
- **WHEN** a client requests the detail endpoint
- **THEN** the response does not include an unbounded related-URL array

### Requirement: Relation matches use stored host columns exactly
Related groups SHALL be resolved by an exact equality match on the stored `urlHost` or `parentDomain` columns for rows whose type carries that column, using the supporting indexes. IP IOC groups, which store no `urlHost` or `parentDomain`, SHALL NOT be related by these queries.

#### Scenario: IP group relation is rejected
- **WHEN** a client requests a relation for an IPv4 IOC group
- **THEN** the endpoint returns a 400 response explaining that IP groups have no host relations

#### Scenario: Apex and subdomain cases resolve by stored parent domain
- **WHEN** `api.example.co.uk` stores parentDomain `example.co.uk` and `example.co.uk` stores parentDomain `example.co.uk`
- **THEN** each group lists the other under `relation=parentDomain`, and neither lists an unrelated suffix host

### Requirement: Relation responses are deterministic and bounded
Relation listings SHALL be ordered by a stable key so that pagination does not skip or repeat groups, SHALL cap pageSize at 100, and SHALL report `totalCount` equal to the number of related groups. A valid group with no relations SHALL return an empty `items` array with `totalCount` 0, not an error. An id that does not resolve SHALL return 404.

#### Scenario: Unrelated host returns empty listing
- **WHEN** no other group shares the resolved group's urlHost
- **THEN** the endpoint returns 200 with empty items and totalCount 0

#### Scenario: Unknown id returns 404
- **WHEN** a client requests relations for a non-existent id
- **THEN** the endpoint returns a 404 not-found problem detail