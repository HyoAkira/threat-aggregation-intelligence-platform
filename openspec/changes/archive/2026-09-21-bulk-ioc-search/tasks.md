## 1. Backend contracts and shared search evaluation

- [x] 1.1 Add bulk request/response contracts, per-input status/error models, summary-count fields, and the bounded request constants; verify the API and application projects compile with the new DTOs.
- [x] 1.2 Add the IOC suggestion response contract and shared limit constants; verify serialization uses the existing lowercase IOC type tokens and nullable/error conventions.
- [x] 1.3 Extract shared page-bound, provider-filter, and IOC classification/normalization evaluation used by `SearchIocsHandler` and the new bulk handler; verify all existing single-search unit tests still pass unchanged.

## 2. Batched IOC query reader

- [x] 2.1 Extend `IIocQueryReader` with a bounded batch-search operation and IOC suggestion catalog operation, updating all test doubles and compile-time implementations; verify `dotnet test --no-restore tests/ThreatIntelligence.UnitTests/ThreatIntelligence.UnitTests.csproj` builds the affected test fixtures.
- [x] 2.2 Implement the batch candidate query in `IocQueryReader` for exact IP/URL identities, explicit-port behavior, domain/URL-host/parent-domain relations, hash-safe value comparison, provider/tag filters, and shared paging; verify reader integration tests cover the same identity through single and bulk paths.
- [x] 2.3 Reuse the existing group hydration and source mapping for batch results, preserving provider fields, tags, aggregate dates, false-positive state, and deterministic ordering; verify port variants and duplicate canonical identities produce correctly attributed groups without row loss.
- [x] 2.4 Implement prefix-based unique IOC suggestions with trimming, case-insensitive matching, deterministic value/type ordering, and a hard limit; verify reader tests cover duplicates, no matches, and limits.

## 3. Bulk handler and HTTP endpoints

- [x] 3.1 Implement `BulkSearchIocsHandler` to remove blank entries, validate the 100-value/2048-character/page bounds, classify each value, reuse canonical work for duplicates, preserve original order, and produce mixed matched/not-found/invalid outcomes and summary counts; verify unit tests cover mixed inputs, duplicate lines, invalid values, filter-eliminated matches, and page metadata.
- [x] 3.2 Add `POST /api/iocs/search/bulk` and `GET /api/iocs/suggestions` to `IocsController` with the existing anonymous authorization policy and shared ProblemDetails handling; verify malformed envelopes and invalid limits return 400 while valid mixed bulk requests return 200.
- [x] 3.3 Add API integration coverage for anonymous access, blank-line handling, 101-value rejection, oversized values, page/pageSize validation, exact and domain-related matches, provider/tag filters, mixed statuses, deterministic input order, and suggestion prefix behavior; verify `dotnet test` passes for the affected integration project.

## 4. Frontend API client and validation

- [x] 4.1 Add Zod schemas and TypeScript types for bulk requests, per-input outcomes, summary counts, paged groups, and IOC suggestions; verify invalid response shapes are rejected by the client schema tests.
- [x] 4.2 Add `searchIocsBulk`, `getIocSuggestions`, and a query key containing the ordered inputs, filters, page, and pageSize; verify API client tests assert the exact HTTP method, request body, query encoding, and response parsing.
- [x] 4.3 Add multiline submission normalization that trims values, removes blank lines, preserves order and duplicates, and selects the existing single-search client for one value or the bulk client for multiple values; verify frontend unit tests reject empty submissions and preserve filters.

## 5. Multiline IOC editor and autocomplete

- [x] 5.1 Implement the accessible multiline IOC editor with one-value-per-line guidance, caret-based active-line detection, and controlled text updates; verify it renders a real labelled textarea and retains arbitrary free-form text.
- [x] 5.2 Add active-line IOC suggestions backed by `getIocSuggestions`, with bounded React Query fetching, loading/empty states, keyboard navigation, pointer selection, and replacement of only the active line; verify component tests cover selection, line preservation, blank lines, and absent suggestions not blocking entry.
- [x] 5.3 Preserve the existing tag and provider autocomplete controls and filter normalization while integrating the new editor; verify existing tag/provider component tests continue to pass.

## 6. Search-page bulk presentation

- [x] 6.1 Update `/search` to submit the multiline editor through the single or bulk path, keep submitted values and filters during loading/errors, and retain the existing single-value result/error presentation; verify route tests assert the single-line regression path and no IOC-type selector.
- [x] 6.2 Add bulk summary and per-input result sections in submission order, rendering matched `IocGroupCard` groups separately from invalid and not-found status explanations; verify route/component tests cover mixed outcomes without false result cards.
- [x] 6.3 Add shared bulk page navigation when any matched item has additional groups, preserving the submitted list and filter state on page changes; verify changing page issues one bulk request with the new page and does not duplicate or reorder input sections.
- [x] 6.4 Add pending, transport-error, retry, and correlation-ID behavior for bulk requests; verify route tests distinguish mixed per-item statuses from page-level request failures.

## 7. Verification and delivery checks

- [x] 7.1 Run targeted backend unit and integration tests for search, query reader, and IOC API behavior; verify existing exact-search, browse, tag-catalog, and response-contract tests remain green.
- [x] 7.2 Run frontend unit tests, lint, type/build checks, and formatting checks; verify search, autocomplete, API-client, and existing browse tests all pass.
- [x] 7.3 Run `openspec validate --change bulk-ioc-search --strict` and review the final change status; verify every required artifact is complete and each task maps to a spec scenario or design decision.
