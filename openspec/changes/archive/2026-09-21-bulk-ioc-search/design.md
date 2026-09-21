## Context

The existing query surface is already split into reusable layers: `IocsController` exposes anonymous IOC routes, MediatR handlers validate and classify inputs, `IIocQueryReader` returns source-attributed `IocGroupResult` pages, and the frontend `search.tsx` route uses React Query with tag and provider autocomplete components. The current exact-search handler and reader are single-identity operations, while the tag catalog already demonstrates a bounded prefix-suggestion endpoint.

The design must preserve the existing `GET /api/iocs/search` and `/api/iocs/tags` contracts, use the shared ProblemDetails errors, keep per-source fields and aggregates intact, and prevent a pasted list from causing unbounded request fan-out or response size. No persistence schema or external dependency is required.

## Goals / Non-Goals

**Goals:**

- Add an additive bulk-search API with bounded input, shared page bounds, deterministic input ordering, mixed per-value outcomes, and existing IOC group responses.
- Reuse one normalization and filter path for single and bulk search so classification, ports, domains, tags, providers, and not-found behavior cannot drift.
- Add bounded IOC-value prefix suggestions suitable for the active line of a multiline editor.
- Extend the existing search page while preserving the current single-value flow, source cards, filters, loading states, and retry behavior.
- Make the plan testable at handler, repository, API, component, and route levels.

**Non-Goals:**

- Changing the existing browse endpoint or its filtering and ordering contract.
- Adding a new IOC type, changing ingestion normalization, or changing the shared `IocGroupResult` shape.
- Persisting search history, exporting results, or adding asynchronous jobs for bulk requests.
- Requiring suggestions for successful submission; suggestions remain advisory and free-form values remain valid.

## Decisions

### 1. Use additive endpoints with a POST body for bulk input

Add `POST /api/iocs/search/bulk` rather than overloading the existing GET query string. A JSON body avoids URL-length limits for pasted lists and leaves the established exact-search endpoint stable for existing clients.

The request model is:

```json
{
  "queries": ["1.2.3.4", "evil.example", "not an IOC"],
  "tag": "botnet",
  "provider": "ThreatFox",
  "page": 1,
  "pageSize": 20
}
```

The response contains ordered item results plus summary counts. Each matched item carries a page of existing `IocGroupResult` values and its `totalCount`; invalid and not-found items carry empty groups and a safe per-item error code/detail. Envelope validation errors remain HTTP 400. Per-item invalid and absent values remain HTTP 200 so one bad line cannot hide valid matches.

Alternative considered: send one GET request per line from the browser. This was rejected because it creates visible partial-loading complexity, increases request fan-out, loses a single ordered response contract, and makes rate/payload limits harder to enforce.

### 2. Extract shared search evaluation and add a bounded batch reader operation

Keep `SearchIocsHandler` as the owner of the existing single-search response behavior, but move classification, page-bound validation, and provider/filter normalization into a shared application-level evaluator used by both the single and bulk handlers. The evaluator returns either a canonical `NormalizationResult` or a per-input invalid outcome; it does not turn a bulk invalid value into an exception that aborts the request.

Extend `IIocQueryReader` with a bulk-oriented operation that accepts the successful canonical identities, one normalized `IocSourceFilter`, and shared page bounds. Its EF Core implementation will:

1. Build one bounded candidate query for the submitted identities, using hash plus stored normalized-value equality for exact IP/URL identities and the existing domain, `urlHost`, and `parentDomain` relations for domain identities.
2. Apply provider and tag predicates before grouping, preserving the existing source-attribution semantics.
3. Hydrate provider names, tags, and provider-specific fields once for the candidate rows, then project the rows into the result set for each canonical identity.
4. Apply the existing deterministic group ordering and page window independently per identity, reusing the same group builder so aggregates and false-positive state remain identical to single search.
5. Map a canonical identity with no filtered groups to a not-found outcome while leaving matched identities independent.

The handler will keep a mapping from each non-empty input position to its canonical identity. Duplicate lines can share classification and reader work, but the final projection repeats the outcome at every original position. This satisfies input-order semantics without launching one unbounded task per line.

Alternative considered: loop over `SearchGroupsAsync` once per line. It is simpler but risks 100 sequential database round trips and repeated hydration. A bounded batch reader keeps the API limit meaningful and makes the performance cost proportional to one candidate query plus bounded in-memory projection.

### 3. Make domain expansion paged per bulk item

Bulk requests use one shared `page` and `pageSize` for every input. This preserves the current search page model when a domain expands to many exact/domain-related groups and gives clients a deterministic payload bound. The frontend initially renders page one; any existing pagination affordance can be reused at the bulk-item level if the implementation exposes navigation for an item with `totalCount > pageSize`.

Alternative considered: return every group for every submitted domain. This was rejected because a small list containing broad parent domains could produce an unbounded response even when the input count is capped.

### 4. Add IOC suggestions as a catalog endpoint

Add `GET /api/iocs/suggestions?q=<prefix>&limit=<1-20>` and a reader method parallel to `GetTagCatalogAsync`. The query groups by normalized `(IocType, IocValue)`, performs case-insensitive prefix matching, returns at most 20 unique values, and applies deterministic value/type ordering. Suggestions are anonymous like the current IOC query routes and carry no provider-row details.

Alternative considered: reuse `/api/iocs/tags` for IOC suggestions. This was rejected because the tag endpoint has a different response contract and data source; a distinct route makes the API contract explicit and avoids overloading tag semantics.

### 5. Use an active-line multiline editor on the existing search page

Replace the single-line IOC input with a textarea-based component that stores the complete editor text. On submit, the page trims and removes blank lines, keeps the remaining order, and chooses the existing single-search client for one value or the new bulk client for multiple values. Tag and provider controls remain unchanged.

Because native `datalist` cannot attach to a textarea by line, the new component will derive the line containing the caret and query suggestions only for that active line after it has non-empty text. It will render an accessible listbox below the editor, allow keyboard selection and pointer selection, replace only the active line, and always leave free-form text editable. React Query cancellation and stale-time behavior will mirror the existing tag autocomplete.

Alternative considered: one datalist attached to a hidden input or suggestions for the entire textarea. Both make line replacement and keyboard semantics ambiguous; an active-line listbox matches the user’s editing model.

### 6. Keep response validation explicit in the frontend

Add API-client request/response types and Zod schemas for the bulk envelope, bulk items, errors, and suggestion entries. Add a query key that includes the ordered input list, filters, and page values so React Query cannot reuse a result for a different pasted list. The result view will render a summary count and one section per input; only matched items render `IocGroupCard` content.

The single-search error mapping remains unchanged. Bulk transport errors use the existing retryable error component, while per-item invalid/not-found statuses are rendered as data rather than thrown errors.

## Risks / Trade-offs

- [Bulk SQL predicate complexity] → Cap the effective input list at 100, deduplicate canonical work internally, use a single bounded candidate query, and cover exact, port, domain relation, and hash-collision behavior with integration tests.
- [Large domain result sets] → Reuse the existing page/pageSize bounds independently for each input and expose `totalCount` so the UI can indicate additional pages without returning them all.
- [Anonymous suggestion disclosure] → Return only normalized value/type pairs already visible through the public IOC search surface; cap prefix length and result count, and do not include provider details or tags.
- [Frontend editor accessibility] → Use a real textarea, labelled active-line suggestions with listbox semantics, keyboard navigation, focus-visible styling, and tests for replacement and free-form entry.
- [Contract drift between single and bulk search] → Centralize classification/filter normalization and share the repository group-building path; add paired single/bulk tests for the same identities and filters.
- [Mixed-status UI complexity] → Treat the bulk request as successful transport when the envelope is valid, render status per item, and reserve page-level error UI for transport or envelope failures.

## Migration Plan

1. Deploy the backend additions and shared search refactor; existing GET search, browse, tag, and provider endpoints remain compatible.
2. Deploy the frontend editor and client changes after the bulk and suggestion routes are available.
3. Validate with API integration tests and frontend route/component tests, including a single-line regression test to confirm the old path remains in use.
4. Roll back by serving the previous frontend and removing the new endpoint usage; no database migration or data backfill is required. The additive backend routes may remain temporarily harmless during rollback.
