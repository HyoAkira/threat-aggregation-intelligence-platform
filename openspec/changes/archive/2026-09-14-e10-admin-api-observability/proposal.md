## Why

Administrators need one protected API surface to operate providers, inspect fetches, change runtime settings, and diagnose failures. PRD FR-007, FR-010, FR-011, and FR-012 define these controls, but the API does not expose them yet.

## What Changes

- Add Admin-only provider dashboard and paginated fetch-history endpoints.
- Add grouped IOC summary counts consistent with E09 grouping.
- Add atomic runtime updates for provider enabled/interval values and global log level.
- Add a safe structured log-tail endpoint and correlated fetch lifecycle events.

## Capabilities

### New Capabilities

- `admin-provider-operations`: provider status, supported modes, and paginated fetch history.
- `admin-runtime-settings`: read and atomically update non-secret runtime settings.
- `admin-observability`: grouped database summary, safe log tailing, and correlated fetch lifecycle events.

### Modified Capabilities

- `fetch-job-contract`: the `FetchJob` message gains a correlation identifier (defaulting to the job identifier) so the API and Worker lifecycle events for one job share a trace.

## Impact

- `src/ThreatIntelligence.Application`: Admin queries, commands, validation, and DTOs.
- `src/ThreatIntelligence.Infrastructure`: SQL readers, settings persistence, log-level application, and safe log parsing.
- `src/ThreatIntelligence.API`: Admin-authorized controllers under `/api/admin/*`.
- `src/ThreatIntelligence.Worker`: structured lifecycle event emission through the E05 lifecycle.
- Tests: Admin authorization, SQL aggregation, settings atomicity, log parsing, and correlation.
- No schema changes and no secret read/write endpoints.
