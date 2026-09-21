## ADDED Requirements

### Requirement: Timeline uses persisted fetch-history counters

The Admin timeline SHALL use the persisted `recordsAdded` and `recordsUpdated` counters from successful fetch-history records as its source of truth. Each qualifying history record SHALL contribute its counters once to the local calendar date obtained by converting its UTC completion time into the requested browser timezone. Queued, running, failed, or unfinished records SHALL not contribute activity values, and reading the timeline SHALL not mutate IOC or fetch-history data.

#### Scenario: Successful provider runs are aggregated
- **WHEN** two successful provider fetches complete on the same requested local date with added and updated counters
- **THEN** that local-date point equals the sum of both records' counters

#### Scenario: UTC storage is converted to the user's local date
- **WHEN** a successful fetch completion is stored in UTC near a UTC midnight boundary
- **THEN** the timeline assigns its counters to the local calendar date produced by the requested timezone, rather than blindly using the UTC date

#### Scenario: Non-terminal or failed runs are excluded
- **WHEN** a range contains queued, running, failed, or unfinished fetch-history records
- **THEN** those records contribute zero to the timeline

#### Scenario: Existing summary semantics remain unchanged
- **WHEN** the timeline is queried after IOC data is ingested
- **THEN** `GET /api/admin/summary` continues to return distinct grouped IOC counts without using timeline counters for client-side recomputation
