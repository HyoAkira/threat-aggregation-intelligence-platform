## Why

The browse page already keeps IOC queries server-paginated, but its bespoke table and pager require route-specific interaction code and provide only a limited table experience. A DataTables-backed surface can provide consistent paging and ordering while retaining the existing URL-restorable filters, source-attributed rows, and bounded backend query contract.

## What Changes

- Replace the browse result table and custom pager with a DataTables-backed table that requests only the active server page.
- Keep the route URL authoritative for filters, ordering, direction, page, and page size so reload, Back, Forward, and shared links restore the same view.
- Preserve the existing filter rail, grouped IOC columns, detail and copy actions, result summary, loading/error/empty behavior, keyboard access, responsive overflow, and visual language.
- Allow ordering only for fields supported by `GET /api/iocs`; keep free-text table search and multi-column ordering out of scope.
- Prevent superseded or out-of-order requests from replacing the current view and ensure one user interaction produces one browse request.
- Keep the existing API envelope and database-level `Count`, `Skip`, `Take`, and page hydration behavior unchanged.

## Capabilities

### New Capabilities

- `browse-table-ui`: A scalable, URL-restorable table interaction model for the existing grouped IOC browse results.

### Modified Capabilities

<!-- No existing API requirement changes. The existing `ioc-browse` and `ioc-query-responses` contracts remain unchanged. -->

## Impact

- Frontend dependencies: unstyled DataTables core and its React integration.
- Frontend browse route, a client-only table boundary, typed request/response mapping, focused table styling, and tests.
- Existing browse URL state and `GET /api/iocs` query parameters remain the integration boundary.
- No backend, database, authentication, or response-envelope change.
