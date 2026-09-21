## Why

The Admin Summary currently shows only point-in-time IOC totals, so administrators cannot tell whether ingestion is adding new indicators, refreshing existing ones, or remaining inactive. Fetch history already records `recordsAdded` and `recordsUpdated` for provider runs, making it possible to expose a useful time series without introducing a second ingestion-tracking system.

## What Changes

- Add a bounded Admin IOC timeline read model/API that aggregates fetch-history `recordsAdded` and `recordsUpdated` values into daily buckets for a requested user-local range while accepting canonical UTC instants and timezone context at the API boundary.
- Support the standard time spans `7 days` and `1 month`, plus a validated custom start/end range based on the user's local calendar, with an explicit maximum range to protect the API and database.
- Keep the existing grouped IOC metric cards and add a Recharts line graph with separate Added and Updated series, user-local date labeling, visible UTC-offset disclosure, zero-filled buckets, and an empty-state message when no fetch data exists.
- Add accessible range controls, custom-date validation, loading/stale/error states, manual refresh, and preservation of the last successful chart data on refresh failure.
- Refresh timeline data on the existing bounded Admin Summary cadence, pause background polling when the page is hidden, invalidate the timeline after a provider fetch reaches a terminal state, and refetch when the detected browser timezone changes.
- Add the Recharts frontend dependency and typed/Zod-validated API contracts; no existing summary endpoint fields are removed or renamed.

## Capabilities

### New Capabilities

- `admin-ioc-summary-timeline`: Provides the time-bounded API/read model and frontend chart behavior for added and updated IOC activity.

### Modified Capabilities

- `admin-observability`: Extend the Admin observability contract with a bounded timeline query over persisted fetch-history counters.
- `admin-frontend-observability`: Extend `/admin/summary` with user-controlled time spans, timeline visualization, and the associated refresh and failure-state behavior.

## Impact

- Backend: Admin application contracts, summary controller/read models, fetch-history querying and indexing as needed, API integration tests, and authorization/validation coverage.
- Frontend: `admin.summary` route, browser timezone/range conversion, Admin Zod schemas and API client, query keys/invalidation, component tests, timezone/DST tests, and accessibility coverage.
- Dependency: add Recharts to `frontend/package.json` and lockfile.
- Runtime behavior: bounded polling, timezone-aware aggregation, and terminal-fetch invalidation add read traffic to the Admin Summary surface; persisted timestamps remain UTC and no changes are required to provider ingestion or IOC upsert accounting.
