## Context

The existing `IocsController` exposes provider names directly from `IIocQueryReader`, while browse and search already normalize tag filters and preserve source attribution. Tags are stored in `tags`, and provider-row associations are stored in `iocTags` with a composite relationship. The tag catalog can therefore be implemented as a bounded read model without schema changes or a new application command.

## Decisions

### API contract

- Add `GET /api/iocs/tags?limit=<1..100>&q=<optional-prefix>` before the `{id}` route.
- Return `IocTagCatalogItem` values: `name` and `usageCount`.
- Count association rows, not distinct indicator values or provider groups. This matches the project's provider-row storage model and makes the number explicit in the response contract.
- Keep the endpoint anonymous like `/api/iocs`, `/api/iocs/providers`, `/api/iocs/search`, and browse.
- Let ASP.NET invalid model state produce the shared 400 problem response for missing/out-of-range limits; use a small explicit normalization path for `q`.

### Query implementation

Add a default method to `IIocQueryReader` so existing test doubles remain source-compatible. The EF implementation projects `Tags` with a correlated `IoCTags.Count`, filters with `StartsWith` after normalization, orders by count descending then name ascending, and applies `Take(limit)` in SQL. The existing unique tag-name index remains sufficient for the prefix lookup; no cache or migration is justified for this bounded read.

### Frontend

Add a Zod schema and `getIocTagCatalog` client function. A small shared `TagAutocompleteInput` uses React Query and a native `<datalist>` so it remains accessible and does not add a UI dependency. It requests a small fixed limit while the user has a non-empty prefix, renders `name` options, and leaves the current text input usable if the request fails. Browse and search retain their existing draft state and submission behavior.

## Verification

- Integration tests assert anonymous access, usage counts, ordering/tie-breaking, prefix normalization, empty matches, and invalid limits.
- Frontend tests assert the client path/schema and that the shared input renders suggestions while preserving manual entry.
- Run targeted .NET and frontend tests, then the relevant full suites if the environment supports them.
