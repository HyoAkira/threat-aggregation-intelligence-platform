# Design

## Context

The existing Admin Summary endpoint and reader return only distinct current IOC counts. Fetch history already stores UTC `StartedAt`/`FinishedAt` values and the persisted `RecordsAdded`/`RecordsUpdated` counters, while the frontend currently has a single React Query summary query with a 30-second refresh interval. Provider job completion currently invalidates the summary query through the Admin provider-operation flow.

The new timeline crosses the API, persistence, frontend query/cache, and visualization layers. The API uses the existing Admin authorization and ProblemDetails behavior, and the frontend validates Admin responses with the existing Zod boundary. See `proposal.md` and the delta specs for the required behavior.

## Goals / Non-Goals

**Goals:**

- Add a bounded daily timeline read model without changing IOC upsert accounting or the existing summary response.
- Keep persisted timestamps and API range boundaries canonical UTC while assigning completed fetches to the requesting user's local calendar using an IANA timezone.
- Handle DST and fractional offsets correctly, including explicit local-time/UTC-offset disclosure in the UI.
- Reuse the existing Admin refresh, query invalidation, authorization, error, and stale-data conventions.
- Keep the chart accessible through semantic labels and a text-equivalent representation of the plotted values.

**Non-Goals:**

- Replacing fetch history or introducing a second counter store.
- Exposing individual fetch-history rows or provider payloads through the timeline endpoint.
- Supporting arbitrary user-selected historical timezones or storing a timezone preference on the account.
- Sub-minute or hourly chart granularity; this change is daily.
- WebSocket/server-push updates; refresh remains bounded polling plus provider-job invalidation.

## Decisions

### 1. Add a dedicated timeline endpoint and read model

Add `GET /api/admin/summary/timeline` alongside the existing summary endpoint. The request carries `from` and `to` as UTC ISO-8601 instants and `timeZone` as the browser's IANA timezone identifier. The response carries the normalized UTC range, the timezone, `hasSuccessfulFetches`, and daily points containing `localDate`, `bucketStartUtc`, `recordsAdded`, and `recordsUpdated`.

The timeline reader is separate from the existing grouped-summary reader so the established `/api/admin/summary` contract and query remain unchanged. The controller can compose both readers without making timeline parameters affect the current metric cards.

Alternative considered: extend the current summary response with a nested timeline. Rejected because it would force every summary refresh to run a bounded history aggregation, complicate independent range caching, and risk breaking the existing response contract.

### 2. Aggregate in the API using the requested timezone

The reader filters successful fetch-history rows by UTC `FinishedAt` in the half-open `[from,to)` interval, projects only the completion timestamp and two counters, and converts each completion instant to the requested timezone before assigning its local calendar date. It fills the complete local-date range in memory, including zero-valued gaps, and derives each local midnight's `bucketStartUtc` using the same timezone rules.

The query is bounded to at most 31 local calendar days. Database filtering remains UTC so SQL Server can use the completion-time index; timezone conversion and local-day grouping happen after the bounded projection because the database query provider cannot reliably translate arbitrary IANA timezone conversion. A new index covering `FinishedAt` and the success status filter should be added if query-plan verification shows the existing indexes are insufficient.

Alternative considered: group by UTC date and convert labels in the browser. Rejected because events around UTC midnight would be assigned to the wrong user's local day. Alternative considered: return raw history rows and group in the browser. Rejected because it exposes more operational data and moves bounded aggregation and DST correctness into every client.

### 3. Use browser local calendar boundaries with UTC transport

The frontend derives the IANA timezone from the browser and computes preset/custom local calendar boundaries. It converts those boundaries to UTC instants for the request, includes the timezone in the query key and request, and displays the returned local dates using the same timezone. The default preset is seven local calendar days; the one-month preset is a 30-day local-calendar window, while custom ranges remain subject to the 31-day API limit.

The offset shown beside the controls is computed for the current browser instant and formatted as `UTC±HH:MM`, including fractional offsets and `UTC+00:00`. The IANA timezone remains the authoritative grouping context; the offset is a user-facing disclosure and not the value sent in place of the timezone. If no valid browser timezone is available, the frontend uses an explicit UTC fallback and labels the chart accordingly.

Alternative considered: send only the numeric offset. Rejected because a fixed offset cannot represent DST transitions across a 7-day or 30-day range. Alternative considered: persist a profile timezone. Rejected because the requirement is about the viewing browser and does not require account settings or migrations.

### 4. Integrate with existing React Query refresh behavior

Add a timeline query key containing the UTC range and timezone. The Summary route keeps the current summary query and adds an independently enabled timeline query once the local range is valid. It uses the existing 30-second visible-page cadence, pauses background polling, preserves the last successful points during a failed refetch, and invalidates all matching timeline queries when the provider-operation flow reaches a terminal fetch state. A timezone change creates a new key and prevents points from the old timezone being shown as current.

Manual refresh invalidates both the existing summary query and the selected timeline query. The existing provider invalidation helper should be extended to invalidate the timeline key prefix so any open range refreshes without requiring the provider page to know the selected date range.

### 5. Use Recharts with an accessible data fallback

Add Recharts as a frontend dependency and render the two validated series with a responsive line chart, UTC/local-aware axis labels, legend, and tooltip. The page also renders a visually hidden or compact accessible data table/text summary containing the selected local range, timezone/offset, each point date, and both counters. This avoids depending on SVG geometry alone for screen-reader users and gives tests a stable representation of chart values.

## Risks / Trade-offs

- [Timezone ID compatibility] → Validate browser IANA identifiers through one server-side resolver, cover the deployed Linux runtime and UTC fallback, and return the existing 400 ProblemDetails contract for unknown identifiers.
- [DST boundary errors] → Test `America/New_York` across spring-forward/fall-back windows and a fractional-offset zone such as `Asia/Kathmandu`; derive bucket starts through timezone conversion rather than fixed 24-hour arithmetic.
- [History query cost] → Enforce the 31-local-day limit before querying, filter by UTC `FinishedAt` and successful status, project only required columns, and verify/add a supporting index.
- [Refresh fan-out] → Invalidate the timeline by a narrow query-key prefix after terminal jobs and retain the existing 30-second polling floor; do not add push infrastructure.
- [Chart library bundle size] → Use only the required Recharts primitives and verify the production bundle during the frontend build.
- [Partial implementation compatibility] → Keep the existing summary cards and endpoint unchanged so the frontend can ship the timeline contract as one coordinated API/UI change.

## Migration Plan

1. Add the timeline DTOs, reader, validation, controller route, timezone resolver, and persistence index/migration if query-plan verification requires it.
2. Add API integration tests for authorization, range validation, local-day aggregation, zero-filled gaps, DST, fractional offsets, and no-successful-fetch responses.
3. Add Recharts, the frontend schemas/client/query key, local-range/timezone helpers, chart UI, accessible fallback, and refresh/invalidation wiring.
4. Add frontend unit/component tests for range conversion, offset formatting, timezone fallback/change, chart states, and refresh continuity; run the existing frontend build, lint, and test suites.
5. Deploy the API and frontend together. Rollback is safe by reverting the frontend route/dependency and leaving the additive timeline endpoint/index in place; the existing summary endpoint and fetch-history writes remain compatible.

