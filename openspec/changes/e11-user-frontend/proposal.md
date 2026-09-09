## Why

The backend query and authentication capabilities need an accessible user interface so users can log in, search, browse, and inspect source-attributed IOCs. PRD user stories US-001–US-006 and Backlog TI-110–TI-116 define the required experience.

## What Changes

- Add a responsive application shell and accessible UI states.
- Add JWT session handling, login, logout, expiry handling, and protected routes.
- Add exact IOC search with validation, tag entry, and grouped results.
- Add IOC detail and paginated host/parent-domain relations.
- Add a filterable, sortable, paginated browse page with URL-persisted state.

## Capabilities

### New Capabilities

- `user-frontend-shell-auth`: responsive shell, login, session handling, and role-aware route guards.
- `user-frontend-search`: exact IOC search and tag-assisted discovery using the E09 contracts.
- `user-frontend-exploration`: source-attributed IOC detail, paginated relations, and grouped browse.

### Modified Capabilities

- None.

## Impact

- `frontend`: React/Vite pages, routing, state, API client, accessible components, and tests.
- Depends on E04 authentication and E09 query/error contracts.
- No backend, schema, or `.env` changes; the development frontend continues to use the `/api` proxy.
