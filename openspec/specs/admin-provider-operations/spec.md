# Admin Provider Operations Specification

## Purpose

Provides administrators with a consistent view of provider state and fetch history without exposing provider credentials or unsafe failure data.

## Requirements

### Requirement: Provider dashboard
`GET /api/admin/providers` SHALL require the Admin role and return every provider with `id`, `name`, `baseUrl`, `isEnabled`, `intervalMinutes`, `lastFetchStatus`, `lastFetchAt`, `nextFetchTime`, `supportedModes`, `defaultMode`, `recentCooldownMinutes`, `fullCooldownMinutes`, and `minimumIntervalMinutes`. Never-run providers SHALL use null status/time fields. The response SHALL NOT include API keys or other secrets.

#### Scenario: Never-run provider
- **WHEN** an Admin lists providers before a provider has completed a fetch
- **THEN** that provider is returned with null fetch times/status and its configured mode list

#### Scenario: Non-Admin is forbidden
- **WHEN** an authenticated User requests the provider dashboard
- **THEN** the API returns 403 ProblemDetails

### Requirement: Paginated provider history
`GET /api/admin/providers/{id}/history` SHALL return `{items,page,pageSize,totalCount}` ordered by `startedAt` descending and then `id` descending. Page defaults SHALL be 1 and 20; `pageSize` SHALL be between 1 and 100. An unknown provider SHALL return 404 and invalid paging SHALL return 400.

#### Scenario: History is newest first
- **WHEN** an Admin requests a provider with multiple fetches
- **THEN** the newest logical fetches are returned first with status, trigger, mode, times, counters, and safe terminal error

### Requirement: Logical job status
`GET /api/admin/fetch-history/{jobId}` SHALL return the single E05 history record for that logical job, including current status, timing, counters, and safe terminal error. An unknown job ID SHALL return 404.

#### Scenario: Running job is addressable
- **WHEN** an Admin requests a known running `jobId`
- **THEN** the response returns that job's Running state without creating another history record
