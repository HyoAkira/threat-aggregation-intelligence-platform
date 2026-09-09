## Context

See proposal.md - Why. PhishTank is a full-dump-only provider (`https://data.phishtank.com/data/online-valid.json.gz`, no required key, quota-limited). The E05 change provides the messaging/guard/history shell: an `IProviderFetchExecutor` seam the consumer calls, the trigger-guard service (including PhishTank rolling-budget enforcement), the fetch-history lifecycle, and the error classifier that already treats 429/quota as non-retryable. E03 supplies the shared normalization service (URL normalization, `urlHost`/`parentDomain` via PSL). E02 defines the schema the adapter writes through E08's pipeline: `phishTankDetails` (`nvarchar(max)`, `ISJSON` check), `target`, `isVerified`, `phishTankOnline`, `firstSeenUtc` (from `submission_time`), `lastSeenUtc` (from `verification_time`). The worker is a .NET 10 hosted process; `IHttpClientFactory` and `IValueHasher`/`TimeProvider` are already registered in Infrastructure DI. `ThreatFoxOptions` demonstrates the configuration pattern; PhishTank currently has no config keys.

## Goals / Non-Goals

**Goals:**
- A streaming PhishTank HTTP client with descriptive UA, timeouts, cancellation, optional app-key support, and typed 429 signaling.
- Whole-feed streaming parse (no whole-file buffering) within a documented memory bound, with a clean failure taxonomy.
- Full record mapping including complete `phishTankDetails` preservation and the contextual-IP rule.
- `fetchesPast24h` exposure feeding the admin dashboard, with window-boundary and retry-exclusion tests.
- End-to-end adapter integration committing once or rolling back entirely on any bad record.

**Non-Goals:**
- No new database schema, migrations, or `.env` keys (no required app key; optional key stays unconfigured by default).
- No ETag/skip-if-unchanged support (deferred per PRD OQ-5).
- No changes to the E05 guard service, error classifier, or history lifecycle; budget enforcement and 429 classification stay there.
- No PhishTank "recent" mode (provider has no recent endpoint).
- No IOC writes from the adapter itself; it returns normalized provider records for E08's atomic pipeline to persist.

## Decisions

- **HTTP client**: Register a named `IHttpClientFactory` client (`PhishTank`) with a descriptive user agent, an explicit timeout, and `HttpCompletionOption.ResponseHeadersRead` so the body streams. Decompress generically from `Content-Encoding` (`gzip`/`identity`; brotli if upstream ever sends it). Stream directly into the parser — never read the whole body into memory. Rationale: factory-managed lifetimes and the project's existing HTTP conventions (Tech Spec §8); the alternative (a hand-managed `HttpClient`) risks socket exhaustion and retry duplication.
- **JSON streaming parse**: Use `JsonSerializer.DeserializeAsyncEnumerable<PhishTankFeedEntry>(stream, ...)` over the decompressed stream so top-level array elements are deserialized one record at a time, each element bounded in size, with no retention of previously-seen records. Rationale: simplest streaming approach that satisfies the memory bound; alternatives (manual `Utf8JsonReader` token walk or a full `JsonDocument` load) trade either complexity or whole-file buffering.
- **Failure taxonomy**: Map exceptions to typed outcomes the E05 classifier consumes: `HttpRequestException`/timeout → transport (retryable); `InvalidDataException` from gzip → decompression (retryable); `JsonException` → parse (retryable — upstream transient corruption); `PhishTankRecordValidationException` carrying record index + reason → record-validation (terminal, no retry); HTTP 429 → `QuotaExceededException` (terminal). Rationale: deterministic classification without changing E05's classifier; the record-validation case aborts the whole batch per FR-009.
- **Record mapping**: Build the provider record through the shared normalization service (URL normalization; extract `urlHost` and PSL `parentDomain`). Map `submission_time` → `firstSeenUtc` and `verification_time` → `lastSeenUtc` (UTC, provider-format parsing with tolerant fallback), `verified`/`online` → typed nullable bools (null when absent, never collapsing valid `false`), `target` → string. `phish_id` → `providerRecordId`, `phish_detail_url` → `providerDetailsLink`. Rationale: matches the E02 schema contract and FR-003's "null properties omitted, valid false/0/empty retained" rule.
- **Details preservation**: Serialize the raw detail array with `System.Text.Json` into a string validated by the DB `ISJSON` check; round-trip tests assert field-level fidelity. Contextual IPs inside details are never routed to the IOC mapper. Rationale: the schema's `phishTankDetails` column exists precisely to retain these fields (Tech Spec §4).
- **Memory bound evidence**: Add a test feeding a fixture ≥2× the sampled full-feed size (capture the real size during a dev fetch and record it) asserting ≤256 MiB additional resident memory, using a process-level or GC-based measurement. Recalibrate the threshold with recorded evidence if the real feed requires it. Rationale: satisfies TI-071's acceptance and keeps the guarantee honest rather than assumed.
- **Budget exposure**: Implement a `FetchCountForProviderSinceAsync(providerId, sinceUtc)` style query on the history repository counting logical fetch starts in the rolling window; `fetchesPast24h = count` for the PhishTank provider, surfaced through the provider dashboard. Enforcement remains the E05 guard; this change only adds the read model and boundary tests. Rationale: single source of truth (`fetchHistory.startedAt`), consistent with Tech Spec §7.
- **Adapter integration**: The PhishTank adapter implements `IProviderFetchExecutor` (full mode only), streaming parse → mapped provider records, returning results or throwing the typed taxonomy exceptions above. The E05 lifecycle/E08 pipeline own transaction, history, and counters. Rationale: keeps DB concerns out of the adapter and lets E07 be tested with fakes before E08 lands; TI-075's end-to-end test proves the seam.

## Risks / Trade-offs

- [Real feed grows past the measured bound] → Mitigation: streaming + per-record retention discipline, plus the recalibrated evidence-based threshold test.
- [PhishTank `verification_time`/`submission_time` format variance] → Mitigation: tolerant UTC parsing with a clear record-validation error naming the offending record.
- [Upstream content-encoding changes] → Mitigation: generic `Content-Encoding` handling; identity tolerated; decompression failures mapped to the retryable decompression class.
- [Detail JSON could exceed column limits on pathological records] → Mitigation: `phishTankDetails` is `nvarchar(max)`; length guard only if evidence shows a need.
- [Duplicate budget logic risk between E07 and E05] → Mitigation: E05 owns enforcement; E07 only adds the read model + tests, documented in both designs.
- [Streaming parse per-element is slower than bulk load] → Mitigation: acceptable for a full-dump cadence (8-hour interval, budget-capped); benchmark noted in tasks.

## Migration Plan

- Additive only: register the PhishTank named HTTP client, the adapter, and the parser in Worker DI; add the `fetchesPast24h` query to the history repository.
- No schema or config changes; no migration. Rolling back removes the adapter registration and the dashboard field; the E05 shell remains untouched.
- The sanitized `online-valid.json.gz` fixture and fake HTTP handler ship with the test projects and are reused by E13's fixture suite.