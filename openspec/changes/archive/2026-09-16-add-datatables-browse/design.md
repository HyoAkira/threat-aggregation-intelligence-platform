## Context

See `proposal.md` for motivation and `specs/browse-table-ui/spec.md` for observable behavior. The browse route currently owns validated URL state, filter drafts, a React Query request, grouped row rendering, and a custom pager. `GET /api/iocs` returns `{ items, page, pageSize, totalCount }`; the repository counts the filtered grouped query, pages group keys in SQL, and hydrates only the selected keys.

TanStack Start renders route components on the server by default, while DataTables is DOM-oriented. The current Jest configuration uses a Node environment and `renderToString`, so it verifies route SSR but cannot execute DataTables effects or interactions.

## Goals / Non-Goals

**Goals:**

- Make DataTables the table paging and supported-column ordering interaction layer while preserving the existing backend contract.
- Keep the URL as durable state and guarantee one browse request per initial view or user interaction.
- Preserve React-rendered links, actions, badges, attribution, error handling, and Tailwind styling.
- Isolate browser-only behavior without disabling SSR for the full browse route.
- Make protocol conversion and state synchronization independently testable.

**Non-Goals:**

- No backend endpoint, pagination algorithm, count query, or response-envelope change.
- No DataTables global search, column search, multi-column ordering, saved local state, responsive extension, or default theme.
- No browser-side full-dataset pagination or sorting of only the loaded page.
- No redesign of the filter rail or IOC row content.

## Decisions

### 1. Use the React wrapper with the unstyled core

Install `datatables.net-react` and the bare `datatables.net` core, register the core with `DataTable.use(DT)`, and do not install or import `datatables.net-dt`. The `-dt` package is DataTables' default styling integration and conflicts with the requirement that the existing Tailwind visual language remain authoritative.

Use the current stable DataTables 3.x core rather than retaining the draft's unexplained DataTables 2 constraint. Pin the accepted major in `package.json` and let `package-lock.json` resolve the exact version. The repository uses npm as the package manager; this change updates `package.json` and `package-lock.json` and removes the unused pnpm lock/workspace files.

Alternatives rejected:

- `datatables.net-dt`: brings the default styling integration that this page does not want.
- Direct imperative DataTables initialization in the route: makes React lifecycle and cell rendering harder to isolate.

### 2. Keep DataTables behind a client-only component boundary

Move DataTables imports and registration into a dedicated client-only table module. Load that module dynamically beneath TanStack Router's `ClientOnly`, with an accessible pending fallback. Keep the route heading, filter rail, validation state, and outer status/error ownership in the isomorphic route component.

The route itself remains SSR-enabled. This avoids importing or initializing DOM-oriented code during Node rendering and avoids removing the whole browse page from the server-rendered document.

Alternative rejected: setting `/browse` to `ssr: false`, because it unnecessarily removes the route shell and filters from SSR and weakens the existing server-render smoke coverage.

### 3. Use one DataTables Ajax function as the sole request owner

Remove the route's table-specific `useQuery` fetch and custom `Pagination` path. The DataTables `ajax(data, callback)` function becomes the only owner of browse data requests. It calls the existing typed `browseIocs(params, signal)` client, which retains Zod response validation and the shared API error model.

Keep transport state outside the rendered cell slots:

- `pending`: current draw is loading;
- `success`: current draw supplied rows and count;
- `empty`: successful current draw supplied no rows;
- `forbidden` or `error`: current draw failed;
- `superseded`: aborted or stale draw, which causes no visible error transition.

On a real failure, settle DataTables' callback sufficiently to clear its processing state, but suppress its empty-table presentation and show the route's existing error surface. A retry invokes one reload for the current URL state. Stale rows must not remain labelled as the failed current view.

Alternative rejected: retaining React Query as a second autonomous fetch path. Two owners would make DataTables draws, cancellation, URL reflection, and request-count guarantees difficult to reason about.

### 4. Encode the server-side protocol in a pure typed adapter

The adapter accepts the DataTables draw request plus the current normalized `BrowseParams` and produces one valid backend request:

- Require integer `draw` and preserve it as a number.
- Require `start >= 0` and a positive supported `length`; reject `length === -1` because “all rows” violates bounded paging and the API maximum of 100.
- Compute `page = Math.floor(start / length) + 1` and `pageSize = length`.
- Read at most the first order term because the browse API has a single-field ordering contract.
- Map only explicit orderable column names or indexes to `lastSeenUtc`, `firstSeenUtc`, `iocType`, or `iocValue`.
- Never forward arbitrary DataTables column data or names to the API.
- Preserve all active type, provider, tag, and observed-period filters.

The response adapter returns:

```text
{
  draw: integer request draw,
  data: result.items,
  recordsTotal: result.totalCount,
  recordsFiltered: result.totalCount
}
```

DataTables defines `recordsTotal` as the count before its filtering and `recordsFiltered` as the count after its filtering. The existing endpoint supplies only the count after the external browse filters. Mapping both fields to `totalCount` is therefore an explicit compatibility choice: external filters define the table's current dataset, and DataTables search/column filtering is disabled. Hide DataTables' stock filtered-count text and retain the existing result summary so the UI does not imply that an unfiltered count was returned. A semantically distinct pair of counts would require an API change, which is out of scope.

### 5. Define URL and table synchronization as a state machine

The normalized route parameters are the durable state. The table instance is not recreated for ordinary route changes.

1. **Initial or shared URL:** initialize `pageLength`, zero-based `displayStart`, and the single order term from the URL. The initial draw issues exactly one request.
2. **Table-originated page or order action:** the draw adapter derives the next normalized parameters, marks their fingerprint as table-originated, updates the URL, and services that same draw with the sole request.
3. **Filter-rail or page-size action:** navigation writes normalized parameters with `page: 1`. The route-state effect applies length, order, and page to the existing table without intermediate draws, then triggers exactly one draw.
4. **Back, Forward, or other external URL change:** if the normalized fingerprint is not the pending table-originated reflection, apply all table state without intermediate draws and trigger exactly one draw.
5. **Reflected URL change:** if the URL fingerprint matches the table-originated guard, consume the guard and do not redraw.

The Ajax closure reads the latest normalized filters through a ref, not a stale initialization-time options object. The synchronization coordinator compares normalized fingerprints and exposes pure transitions so request-count behavior can be unit tested.

Alternatives rejected:

- Recreate the table on every URL change: loses instance state and risks duplicate initialization and requests.
- Let DataTables save state locally: creates a second durable state that can disagree with the shareable URL.

### 6. Combine cancellation with draw ordering

Keep one `AbortController` for the active table request. Abort it before starting a newer request and on unmount. Treat `AbortError` as supersession, not failure.

Cancellation is only resource cleanup; the DataTables integer `draw` remains the ordering guarantee. Update visible metadata or errors only when the completing request is still the latest draw, and always return the same integer draw through the successful callback. A late completion from a transport that ignored cancellation cannot overwrite newer rows or status.

### 7. Render dynamic cells through React slots

Provide row objects through DataTables' Ajax data and define columns explicitly. Use React `slots` for the indicator link/copy action, IOC type badge, threat label, providers, formatted dates, and Analyze link. JSX children define only the static table header; do not map reactive rows into `<tbody>`, because React and DataTables must not both control the same row DOM.

Mark only `iocValue`, `iocType`, `firstSeenUtc`, and `lastSeenUtc` orderable. Provider, threat-label, and action columns are presentation-only and set `orderable: false` and `searchable: false`. Use `orderMulti: false`, `searching: false`, and `stateSave: false`.

### 8. Keep page size and result information in the existing route UI

The filter-rail page-size select remains the only page-length control. Configure DataTables' layout to expose its pager but not its search, page-length, or stock information controls. Keep the existing result summary outside DataTables and update it only from the latest successful draw.

Add focused CSS for the generated DataTables wrapper, processing indicator, ordering affordances, and pager using the current design tokens. Retain the table's horizontal overflow container and visible focus treatment; do not import DataTables theme CSS.

### 9. Split verification by runtime capability

- Keep pure adapter and state-machine tests in the current fast Node suite.
- Keep an SSR test proving that rendering/importing the browse route does not access `window` or `document` and that the client-only fallback is accessible.
- Add a DOM-capable test file using a jsdom environment (and the separately installed matching Jest environment package) or an equivalent real-browser test for DataTables initialization, slots, pager/order interactions, cancellation, history restoration, and exact request counts.
- Retain the existing backend integration evidence for grouped count, stable ordering, `Skip`/`Take`, and selected-key hydration; no new backend behavior is introduced.

## Risks / Trade-offs

- **[Bare DataTables core still generates structural DOM that needs styling]** → Cover wrapper, ordering, processing, and pager classes with focused token-based CSS and a narrow/wide browser smoke check.
- **[URL reflection or a route effect can issue a second draw]** → Use normalized fingerprints, a table-originated reentrancy guard, batched no-draw API updates, and exact request-count tests.
- **[A changing callback or options object can capture stale filters]** → Initialize once and read the latest normalized route state from refs.
- **[Unsupported or multiple order terms can leak into the API]** → Disable multi-order, mark unsupported columns non-orderable, and use a closed allowlist in the pure adapter.
- **[Count fields can imply an unavailable unfiltered total]** → Document the compatibility mapping, disable DataTables filtering, and render only the existing server matching-count summary.
- **[Cancellation can race with completion]** → Combine AbortController cleanup with latest-draw guards and the DataTables draw response field.
- **[Client-only integration can regress SSR or hydration]** → Isolate imports, render a deterministic fallback, and require both Node SSR and production build checks.
- **[Node-only tests can give false confidence]** → Put protocol/coordinator logic in pure units and add DOM/browser coverage for the actual integration.

## Migration Plan

1. Add and lock the React wrapper, bare DataTables 3.x core, and DOM-test dependency if jsdom is selected.
2. Implement and unit-test the pure protocol adapter and URL/table state coordinator.
3. Add the client-only DataTables component with slots, single Ajax owner, request guards, and focused styles.
4. Replace the route's custom table fetch/pager while retaining its filters and status presentation.
5. Run focused Node and DOM/browser tests, formatting/lint, production build, and the existing backend paging tests.

Rollback restores the current browse table, React Query request, and `Pagination` component and removes the new frontend dependencies/styles. The API and database require no rollback.
