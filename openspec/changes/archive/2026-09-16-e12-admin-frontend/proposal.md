## Why

E10 defines the protected administration APIs, but administrators still need a safe, accessible interface for provider operations, fetch history, database metrics, logs, and runtime settings. E12 completes the PRD administration workflows (US-004 through US-008) and the Backlog TI-120 through TI-125 without moving authorization or operational rules into the browser.

## What Changes

- Add an Admin-only provider dashboard with live status, cadence, cooldown, supported-mode, and manual-fetch controls.
- Add paginated fetch-history presentation with safe, readable terminal errors and logical-job status refresh.
- Add database summary and bounded structured-log views.
- Add an atomic runtime-settings form with validation, unsaved-change protection, and application-time feedback.
- Add explicit loading, empty, stale, forbidden, conflict, and failure states for every administration workflow.

## Capabilities

### New Capabilities

- `admin-frontend-provider-operations`: Admin routes for provider status, cooldown-aware manual fetches, and paginated fetch history.
- `admin-frontend-observability`: Admin presentation of grouped database counts and bounded structured logs.
- `admin-frontend-runtime-settings`: Safe editing of non-secret provider and logging settings with atomic-save feedback.

### Modified Capabilities

- None.

## Impact

- `frontend/src/routes/admin`: new provider, summary, logs, and settings routes guarded by the E11 Admin session policy.
- `frontend/src/features/admin` and shared UI: typed E10 clients, query/mutation state, safe text presentation, forms, and reusable status components.
- Frontend tests: component, navigation, mutation-race, conflict, stale-state, and accessibility coverage.
- Depends on E11 shell/auth behavior and an E10 dashboard/settings contract that supplies authoritative per-mode trigger state, default mode, and provider minimum intervals; E12 itself makes no database, provider-execution, or secret-management changes.
