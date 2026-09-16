## 1. Dependencies and Protocol Adapter

- [x] 1.1 Add compatible `datatables.net-react` and bare `datatables.net` 3.x dependencies with npm, do not add `datatables.net-dt` or theme CSS, update `package.json` and `package-lock.json`, and verify `npm ci` succeeds from `frontend/`.
- [x] 1.2 Define typed DataTables request/response and supported-column mapping boundaries, and verify the allowlist contains only `iocValue`, `iocType`, `firstSeenUtc`, and `lastSeenUtc` while providers, threat labels, and actions remain non-orderable.
- [x] 1.3 Implement the pure request adapter for integer `draw`, non-negative `start`, supported positive `length`, first-order-term mapping, and active-filter preservation; verify unit tests cover initial/nonzero pages, every allowed size and sort field, unsupported and multiple order terms, and rejection of `length=-1` or invalid values.
- [x] 1.4 Implement the response adapter returning the integer `draw`, `items` as `data`, and the documented matching-count compatibility mapping for both DataTables count fields; verify unit tests cover populated, empty, filtered, and large-total responses.

## 2. URL and Request Coordination

- [x] 2.1 Implement a pure normalized fingerprint/state-transition coordinator for initial URL state, table-originated page/order actions, filter/page-size resets, reflected navigation, and external Back/Forward state; verify transition tests prove when exactly one draw is required and when a reflected URL requires none.
- [x] 2.2 Implement the sole DataTables Ajax request owner using the existing `browseIocs` client, latest browse-parameter refs, and URL navigation guard; verify one initial request and exactly one request for each page, page-size, ordering, filter, Back, and Forward transition.
- [x] 2.3 Add one active `AbortController`, unmount cleanup, integer-draw freshness checks, and silent `AbortError` handling; verify tests prove superseded requests are aborted, late results cannot replace the latest rows or metadata, and unmount cancellation does not show an error.
- [x] 2.4 Map current-draw pending, success, empty, forbidden, retryable error, and superseded outcomes into the existing route states; verify real failures are distinct from empty results, stale rows are not labelled current, and Retry performs one request for the current URL.

## 3. Client-Only DataTables Surface

- [x] 3.1 Add a dynamically loaded client-only table module that registers `datatables.net` with the React wrapper beneath `ClientOnly`; verify the browse route renders an accessible deterministic fallback in Node without accessing `window` or `document`.
- [x] 3.2 Configure server-side processing with URL-derived `displayStart`, `pageLength`, and single-column order; disable global search, column search, multi-order, local state saving, and the built-in page-length/info controls; verify initialization requests the URL-represented page once.
- [x] 3.3 Define DataTables columns and React slots for the indicator link/copy action, type badge, threat label, providers, dates, and Analyze link without a reactive JSX `<tbody>`; verify grouped source attribution and accessible row actions render from Ajax row objects.
- [x] 3.4 Replace the custom table, route-owned React Query fetch, and `Pagination` path while preserving the filter rail, filter drafts, URL serializer, result summary, date formatting, copy toast, and existing empty/error behavior; verify there is one active browse-data request owner.
- [x] 3.5 Add focused token-based styling for the DataTables wrapper, header ordering affordances, processing state, pager, focus states, and horizontal overflow without importing theme CSS; verify narrow and wide browser smoke checks show no page-level horizontal overflow or default-theme regression.

## 4. Frontend Verification

- [x] 4.1 Update the existing Node browse tests for the client-only fallback and add pure adapter/coordinator suites; verify the relevant tests pass with `npm test -- --runInBand` from `frontend/`.
- [x] 4.2 Add a DOM-capable jsdom test file with the matching Jest environment dependency, or equivalent browser integration coverage, for slot rendering, accessible actions, processing/empty/403/error/retry states, exact request counts, cancellation, page/order/size/filter interactions, and Back/Forward restoration; verify the new interaction suite passes.
- [x] 4.3 Run `npm run check`, `npm run lint`, and `npm run build` from `frontend/`, verifying formatting, types/lint, client-only import isolation, hydration-safe SSR output, and the production bundle all succeed.

## 5. Backend Regression Verification

- [x] 5.1 Run the existing browse handler, API, and repository tests that cover accepted query values, filtered grouped counts, deterministic ordering, stable pages, SQL `Skip`/`Take`, and selected-key hydration; verify they pass without backend or response-contract changes.
- [x] 5.2 Confirm through a focused browser/network smoke check that every table draw calls only `GET /api/iocs` with the normalized URL filters and requests no more than 100 groups; verify no request attempts full-dataset pagination or an unsupported order field.
