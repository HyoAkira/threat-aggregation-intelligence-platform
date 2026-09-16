# `add-datatables-browse` fact check

Checked 2026-09-16 against first-party documentation, upstream source, and the repository's current code. This note does not change the OpenSpec artifacts.

## Executive corrections

1. **Make the dependency decision exact.** For a deliberately unstyled integration, use `datatables.net-react` plus the JS-only `datatables.net` core and call `DataTable.use(DT)`. Do not use `datatables.net-dt`: `-dt` is the default styling integration and its package declares CSS. The upstream React guide normally demonstrates `-dt`, but DataTables' own technical note explicitly demonstrates registering bare `datatables.net`, and the core package says it contains core software only. Also decide whether the proposal truly requires major 2: the official package currently publishes 3.x, so “DataTables 2” requires an explicit 2.x range rather than an unconstrained/latest install. Sources: [React integration](https://datatables.net/manual/components/react/), [React technical note 23](https://datatables.net/manual/core/tech-notes/23), [core npm package](https://www.npmjs.com/package/datatables.net), [styling package model](https://datatables.net/manual/core/styling/frameworks).

2. **Add `draw` to the adapter contract.** DataTables sends `draw`, `start`, `length`, and `order[]`; the callback response must include the same draw counter (as an integer), `recordsTotal`, `recordsFiltered`, and `data`. The current design/tasks mention every required response field except `draw`. Without it, out-of-order response protection is incomplete. Source: [server-side processing protocol](https://datatables.net/manual/core/server-side).

3. **Do not configure `ordering: false` globally.** With `serverSide: true`, a user order action produces a server request; supported columns therefore need ordering enabled. Mark only unsupported columns `orderable: false`, use an explicit index/name-to-API-field allowlist, and accept a single order term (or define deterministic handling for extra terms). `searching: false` is appropriate because the product has no DataTables global search. Source: [sent server-side parameters](https://datatables.net/manual/core/server-side).

4. **Use React slots, not a reactive JSX `<tbody>`.** The official wrapper warns that React and DataTables must not both own reactive row DOM. Supply Ajax/column data and render links, buttons, badges, and other React cell content with `slots`; JSX children should define the header/footer only. The wrapper implements slots as React portals and destroys the DataTable on unmount. Sources: [React integration](https://datatables.net/manual/components/react/), [wrapper source](https://github.com/DataTables/React/blob/master/src/components/DataTable/index.tsx).

5. **Specify one request owner and one synchronization algorithm.** “Recreated or refreshed when route state changes” is too vague and can double-fetch: a DataTables interaction already triggers its Ajax callback, while a URL navigation can remount/reload it again. Keep the custom DataTables `ajax(data, callback, settings)` function as the sole fetch owner; it should translate the draw to API params, update the URL for user-originated table state without causing a second draw, fetch, and invoke the callback. For Back/Forward or an externally supplied URL, apply `pageLength`, order, and zero-based display start to the existing API instance and perform exactly one draw. Add a reentrancy/source guard and test request count, not just final state. DataTables' wrapper captures initial `options` in a ref and initializes only once, so later URL state does not become active merely by passing a new options object. Sources: [custom Ajax function contract](https://datatables.net/reference/option/ajax.html), [wrapper initialization source](https://github.com/DataTables/React/blob/master/src/components/DataTable/index.tsx), [TanStack navigation/search API](https://tanstack.com/router/latest/docs/guide/navigation).

## Protocol details the adapter should encode

- Convert `start` (zero-based) and positive `length` to `page = floor(start / length) + 1`, `pageSize = length`. Reject or normalize `length === -1`; the API caps page size at 100 and “all rows” defeats the stated server-paging goal.
- Initialize DataTables with `pageLength = URL.pageSize`, a matching length menu, `displayStart = (URL.page - 1) * URL.pageSize`, and the URL-derived order. Make unsupported/action columns non-orderable.
- Return `{ draw: Number(request.draw), data: result.items, recordsTotal, recordsFiltered }`.
- The repository's `PagedResult.TotalCount` is explicitly the count of groups **matching the request** (`PagedResult.cs`), not an unfiltered database total. DataTables defines `recordsTotal` as before filtering and `recordsFiltered` as after filtering. Mapping both to `totalCount` is operationally acceptable only if external browse filters are treated as defining the table's entire dataset and DataTables search is disabled; it is not a semantically faithful pair of counts. Document that choice and hide/replace DataTables' stock “filtered from N” information text. A faithful pair would require an additional unfiltered count/API field, which conflicts with the stated no-backend-change and one-request goals. Sources: [DataTables returned fields](https://datatables.net/manual/core/server-side), repository `src/ThreatIntelligence.Application/Features/Iocs/Queries/PagedResult.cs`.
- Preserve all active browse filters on every Ajax call. Sorting changes and page-size/filter changes reset to page 1; simple page navigation does not.
- Preserve the existing backend order tie-breakers. Only map the four accepted API fields (`lastSeenUtc`, `firstSeenUtc`, `iocType`, `iocValue`) and `asc|desc`; never pass arbitrary `columns[i].data` or `columns[i].name` to the API.

## Cancellation, stale responses, and failures

- Keep one `AbortController` per table instance/current request; abort the previous request before starting the next and on component cleanup. `browseIocs` and `apiFetch` already accept an `AbortSignal`.
- Cancellation alone is not the ordering guarantee. Return the integer `draw`; DataTables defines it specifically to prevent asynchronous responses from being rendered out of sequence. Ignore `AbortError` as a superseded request, and ensure it does not become the visible error state.
- The custom Ajax function owns error translation because DataTables only requires that it eventually call the supplied callback with data. Define how `processing` clears and how the existing 403/general error states are shown. Do not convert a real error into a successful empty result, because that violates the spec's empty-versus-error distinction. Sources: [custom Ajax callback](https://datatables.net/reference/option/ajax.html), [Ajax error behavior](https://datatables.net/manual/core/tech-notes/7), [server-side `draw`](https://datatables.net/manual/core/server-side).

## TanStack Start / SSR risk

- TanStack Start renders route components on the server by default, and its code is isomorphic unless constrained. DataTables is DOM-oriented; its wrapper creates the instance in `useEffect`, but its slots call `document.createElement`, so do not assume arbitrary DataTables integration code is server-safe. Sources: [TanStack Start selective SSR](https://tanstack.com/start/latest/docs/framework/react/guide/selective-ssr), [execution model and `ClientOnly`](https://tanstack.com/start/latest/docs/framework/react/guide/execution-model), [wrapper source](https://github.com/DataTables/React/blob/master/src/components/DataTable/index.tsx).
- Prefer isolating the DataTables surface behind TanStack Router's `<ClientOnly>` (with an accessible loading/static fallback) or a client-only module boundary, rather than setting the whole `/browse` route to `ssr: false`. Disabling route SSR would also remove current server-rendered browse content and invalidate the existing SSR test expectations.
- Add an explicit build/SSR smoke assertion: importing/rendering the route on Node must not touch `window`/`document`, and `pnpm build` must succeed. Then verify hydration/client initialization separately.

## Test-plan corrections for this repository

The current Jest config uses `testEnvironment: 'node'`, and `browsePage.test.tsx` uses `renderToString`; effects do not run there, so those tests cannot initialize DataTables, click its pager/header, verify aborts, or detect double requests. Keep the fast Node tests for pure translation and SSR fallback, but add a real DOM suite (per-file `@jest-environment jsdom` plus the separately installed `jest-environment-jsdom`, and preferably Testing Library) or a browser integration test. Jest documents both the per-file environment pragma and the separate jsdom package. Sources: [Jest test environments](https://jestjs.io/docs/test-environment), [Jest DOM testing setup](https://jestjs.io/docs/tutorial-jquery), repository `frontend/jest.config.js` and `frontend/src/routes/browsePage.test.tsx`.

Minimum assertions:

- pure request mapping: initial page, nonzero `start`, allowed sizes, `length=-1`, supported/unsupported and multiple order terms, all filters retained;
- pure response mapping: integer `draw`, empty page, filtered `totalCount`, and correct DataTables counts under the documented external-filter interpretation;
- DOM/browser: slots render real IOC links and accessible copy/analyze actions; one initial request; exactly one request per page/size/order action; filter reset to page 1; Back/Forward restores the DataTables controls and rows with one request;
- concurrency: first request is aborted, late first response cannot overwrite the second, unmount aborts in flight, and `AbortError` does not show an error;
- states: pending/processing, empty, 403, generic error/retry, and no stale rows labeled as fresh;
- SSR/build: client-only fallback is valid accessible markup and no DOM global is accessed during server render.

## Recommended wording change in the OpenSpec

Replace “DataTables is initialized from that state and is recreated or refreshed when the route state changes” with an explicit state machine:

> The URL is the durable state. A DataTables user event updates the URL and is serviced by that same draw's sole Ajax request. An external URL change (initial load, reload, Back, Forward, shared link) is applied to the existing table and triggers exactly one draw. A source/reentrancy guard prevents URL reflection from triggering a duplicate draw. Every response echoes the integer draw value, and the prior request is aborted on supersession or unmount.

This turns the highest-risk part of the proposal into testable behavior rather than an implementation hint.
