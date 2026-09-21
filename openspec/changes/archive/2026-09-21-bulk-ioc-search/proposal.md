## Why

Users currently have to search IOC values one at a time through the exact-search page, or switch to filtered browse when they need broader discovery. This makes investigating a pasted list from a feed, incident report, or analyst note slow and prevents the UI from explaining which individual values were found, absent, or invalid. A bounded bulk-search flow on the existing search page will make list triage practical while reusing the established normalization, source filtering, grouping, and tag-suggestion behavior.

## What Changes

- Add a dedicated `POST /api/iocs/search/bulk` endpoint that accepts a bounded list of IOC values plus the existing optional tag and provider filters.
- Return one ordered outcome per submitted non-empty line, with grouped source-attributed results for matches and explicit invalid/not-found outcomes for values that cannot be searched or are not present.
- Reuse the existing IOC classifier, normalization rules, provider/tag filtering, grouping, pagination limits, error shape, and `IocGroupResult` response model rather than creating a second matching semantic.
- Extend the `/search` page's IOC value control from a single-line input to an accessible multiline editor with one IOC per line, preserving the existing single-value experience and rendering bulk outcomes in submission order.
- Add bounded IOC-value prefix suggestions, modeled on the existing tag catalog endpoint, so the active line can offer autocomplete suggestions without blocking free-form entry.
- Add backend, integration, frontend, and component tests for request limits, normalization, mixed outcomes, ordering, filters, autocomplete, loading, and error states.

## Capabilities

### New Capabilities

- `ioc-bulk-search`: Bounded multi-value search API with per-input outcomes and deterministic result ordering.
- `ioc-search-suggestions`: Anonymous bounded IOC-value prefix suggestions for the search editor.

### Modified Capabilities

- `ioc-search`: Extend the search contract so its normalization, matching, filters, and source-attributed groups are explicitly reusable by bulk search while preserving the existing exact-search behavior.
- `user-frontend-search`: Replace the single-value-only input contract with multiline bulk entry, per-line result states, and IOC-value autocomplete.

## Impact

- Backend: `IocsController`, new bulk query/handler/result contracts, `IIocQueryReader` and `IocQueryReader`, shared normalization/filtering paths, and API/integration tests.
- Frontend: `/search`, API client types and schemas, query keys, a multiline autocomplete input/result presentation, and route/component tests.
- API consumers: the existing `GET /api/iocs/search` contract remains available; the new bulk endpoint and suggestion endpoint are additive.
- Persistence/query performance: bulk requests need a hard item limit and bounded database work so pasted lists cannot create unbounded query fan-out.
