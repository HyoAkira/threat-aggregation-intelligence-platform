# Tasks

## 1. Backend contract and timezone validation

- [x] 1.1 Add Admin timeline query/point/response contracts and a separate timeline-reader abstraction; verify the API and application projects compile with the additive contract.
- [x] 1.2 Implement validation for UTC `from`/`to` instants, IANA `timeZone`, half-open range semantics, and the 31-local-calendar-day maximum; verify malformed, inverted, overlong, and unknown-timezone requests return 400 ProblemDetails without querying history.
- [x] 1.3 Add the timeline route to the existing Admin summary controller and register its reader/resolver dependencies; verify Admin requests succeed and non-admin requests receive the existing 403 behavior.

## 2. Fetch-history aggregation and persistence

- [x] 2.1 Implement the timeline reader to filter successful completed fetches by UTC `FinishedAt`, project only completion/counter fields, convert completion instants into the requested timezone, and sum each counter once per local date; verify successful, failed, running, queued, and unfinished records behave as specified.
- [x] 2.2 Generate zero-filled points for every local date in the requested interval, preserve `hasSuccessfulFetches`, and calculate DST-aware `bucketStartUtc` values; verify gaps, zero-counter successes, UTC-midnight crossings, and local date ordering.
- [x] 2.3 Add or verify a `FinishedAt`/status-supporting fetch-history index and migration only as needed by the query plan; verify migration application and schema mapping tests pass without changing fetch-history writes.
- [x] 2.4 Preserve the existing grouped summary reader and response contract; verify the current distinct IOC summary integration tests still pass unchanged.

## 3. Backend test coverage

- [x] 3.1 Add API/integration coverage for timeline authorization, response shape, range validation, and no-successful-fetch responses; verify the new endpoint returns the existing safe error/correlation contract.
- [x] 3.2 Add timezone aggregation tests for `Europe/Istanbul`, a DST transition zone such as `America/New_York`, UTC, and a fractional-offset zone such as `Asia/Kathmandu`; verify local-day assignment and bucket starts.

## 4. Frontend transport and timezone helpers

- [x] 4.1 Add the Recharts dependency and update the frontend lockfile; verify dependency installation and the production frontend build succeed.
- [x] 4.2 Add Zod schemas/types for the timeline response, an API-client function for UTC `from`/`to` plus `timeZone`, and a query key containing the full range/timezone context; verify schema rejection of malformed points and API-client URL encoding tests.
- [x] 4.3 Add browser-timezone helpers that detect the IANA timezone, format `UTC±HH:MM` including fractional offsets, compute local preset/custom calendar boundaries, and explicitly fall back to UTC; verify positive, negative, fractional, zero, DST, and unavailable-timezone tests.

## 5. Admin Summary timeline UI

- [x] 5.1 Extend `/admin/summary` with an independently loaded timeline query while preserving the existing metric cards and summary refresh behavior; verify the route still renders current summary counts when timeline data is loading or unavailable.
- [x] 5.2 Add accessible 7-day, 1-month, and custom local-date controls with a 7-day default; verify local range conversion, 31-day validation, inverted-date feedback, and no request while the custom range is invalid.
- [x] 5.3 Render Added and Updated Recharts lines with local-date labels, local-time tooltips, zero-filled gaps, legend, and visible timezone/offset disclosure; verify the accessible text/table representation exposes every point and both series.
- [x] 5.4 Add timeline loading, empty, stale, error, retry, and manual-refresh states; verify the last successful points remain visible and are marked stale after a failed refetch.
- [x] 5.5 Make the timeline query include timezone context and react to browser timezone changes without displaying points from the previous timezone as current; verify the query key and rendered disclosure update together.

## 6. Realtime refresh and integration wiring

- [x] 6.1 Configure visible-page timeline polling at no more than once every 30 seconds and pause it while the document is hidden; verify visibility changes do not issue background polling requests.
- [x] 6.2 Extend the existing provider terminal-job invalidation helper to invalidate the timeline query prefix while preserving the selected local range and timezone; verify a terminal fetch refreshes the selected timeline once.
- [x] 6.3 Wire manual Summary refresh to invalidate both current summary and selected timeline queries; verify duplicate refresh actions are suppressed while requests are pending.

## 7. Verification and repository consistency

- [x] 7.1 Add or update frontend component, accessibility, API-client, schema, and timezone tests; verify `npm test`, `npm run lint`, and `npm run build` pass.
- [x] 7.2 Run the affected .NET unit/integration tests and the full solution test suite; verify existing Admin summary/provider-operation behavior remains green.
- [x] 7.3 Run `openspec validate admin-ioc-summary-timeline --type change --strict` and review the final API/UI behavior against every scenario in the three delta specs.
- [x] 7.4 Run `graphify update .` after implementation so the repository knowledge graph reflects the new endpoint, reader, frontend query, and chart relationships.
