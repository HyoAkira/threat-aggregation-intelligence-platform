## 1. Pipeline service and identity resolution

- [ ] 1.1 Define `IIocBatchWriter` (Application) with `ApplyAsync(records, providerId, ct) -> (added, updated)` and register it in Worker/Infrastructure DI, and verify a unit test asserts the interface resolves and returns an added/updated result (TI-080)
- [ ] 1.2 Implement identity resolution by provider-native `(providerId, providerRecordId)` first, then endpoint identity `(providerId, iocType, iocValueHash, port)`, confirming every hash-based match by comparing stored `iocValue` to the incoming value, and verify unit tests cover new, repeat, and cross-provider cases (TI-080)
- [ ] 1.3 On a resolved provider-record row whose new normalized identity belongs to a different provider row, throw an integration-conflict error that aborts the batch rather than merging, and verify a unit/integration test proves the conflict rejects the fetch (TI-005, TI-080)

## 2. Provider-row upsert

- [ ] 2.1 Implement the upsert apply step that preserves `createdAt` and stored `firstSeenUtc`, refreshes `updatedAt`, overwrites provider metadata, and replaces `lastSeenUtc`, and verify a unit test proves a re-fetch changes only the specification-allowed fields (TI-081)
- [ ] 2.2 Implement added/updated counter semantics per TI-004: an existing row increments `updated` even on identical values, a new row increments `added`, and verify `added + updated == committed records` in a unit test (TI-004, TI-081)

## 3. Tag replacement

- [ ] 3.1 Load normalized tag names/IDs `AsNoTracking` into a scalar `Dictionary<string,int>` at fetch start, and verify a unit test asserts the cache loads without tracking entities (TI-082)
- [ ] 3.2 Normalize, lowercase, and deduplicate tag names per chunk, insert each missing name once via `SaveChanges`, update the dictionary, and reference IOC-tag rows by scalar ID, and verify a unit test proves a tag first created in one chunk is reused without error by a later chunk (TI-082)
- [ ] 3.3 Replace each upserted row's tag associations so removed tags disappear while shared tag rows remain, and verify a unit test covers removed and retained tags with source attribution (TI-082)

## 4. Whole-batch transaction

- [ ] 4.1 Implement chunked processing (~2,000 records per chunk) with `AddRange`/`SaveChanges` and `ChangeTracker.Clear()` inside one outer transaction, keeping `FetchHistory` outside it, and verify a unit/integration test proves tracked entity count stays bounded (TI-083)
- [ ] 4.2 Verify a bad record, including a final record, rolls back all IOC and tag changes from every chunk and leaves Failed history with zero committed rows, and verify an integration test proves no partial writes and that earlier-chunk tag insertions roll back (FR-009, TI-083)

## 5. Idempotent redelivery

- [ ] 5.1 Handle unique-key races by relying on the E02 unique indexes and re-resolving once on a constraint violation, and verify repeated and concurrent delivery of the same job produces the same final dataset with no duplicate rows or false added counts (TI-084)
- [ ] 5.2 Verify a losing delivery contributes no added/updated count and redelivery of a committed job does not double-count, and verify an integration test proves single-delivery equivalence (TI-084)

## 6. False-positive state

- [ ] 6.1 Implement the query-time false-positive helper using the shared UTC `TimeProvider` (`updatedAt < now - 180 days`), not persisted, and verify before/on/after 180-day boundary unit tests pass (TI-085)
- [ ] 6.2 Verify a successful reappearance upsert refreshes `updatedAt` and clears the calculated false-positive state, and verify a unit test covers the reappearance case (TI-085)

## 7. Adapter integration and verification

- [ ] 7.1 Wire the worker consumer to invoke `IIocBatchWriter` through the E05 `IProviderFetchExecutor` seam for both ThreatFox and PhishTank, and verify an end-to-end fixture fetch commits valid data once with correct counters (TI-065, TI-075, TI-083)
- [ ] 7.2 Run `dotnet build` and the complete unit/integration test suite, including the SQL-backed transaction/upsert/grouping tests (TI-138), and verify all E08 tests pass with no schema or `.env` changes