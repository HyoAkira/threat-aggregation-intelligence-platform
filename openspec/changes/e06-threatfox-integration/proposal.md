## Why

ThreatFox is one of the two required P0 sources (PRD success metric: ≥2 integrated providers), yet no ThreatFox adapter exists in the solution today — only the domain model (IoC entity, Provider seed id 1), the shared normalization/validation layer (E03), and the provider config key (`ThreatFox:ApiKey`). Without E06, the worker cannot fetch ThreatFox recent or full exports, so no ThreatFox data can be normalized, deduplicated, or searched, blocking the first vertical slice (M2).

## What Changes

- Add an authenticated ThreatFox HTTP client that constructs the recent/full export URLs from the configured base URL and key without logging the key, uses a descriptive user agent and explicit timeout, streams responses, and honors cancellation. It performs no independent retries (job-level retries are owned by MassTransit in E05).
- Add recent-export ingestion that streams and parses the ThreatFox CSV (domains, URLs, `ip:port`) and returns normalized provider records, mapping all supported IOC types.
- Add full-export ingestion that streams and decompresses the `.csv.zip` archive entry-by-entry without loading the whole archive in memory, validates the expected archive structure, and returns normalized provider records.
- Add ThreatFox metadata/tag mapping that retains every applicable value (including `false`, `0`, empty) into the canonical provider record, including native ID, details link, timestamps, threat/malware fields, confidence, booleans, reporter, tags, and port.
- Add the ThreatFox-specific provider validator implementing the E03 provider-validator extension point (fail-closed when a record is not ThreatFox).
- Verify cadence/failure behavior at the adapter boundary: upstream 429/5xx/timeout/cancellation are surfaced for E05/E08 classification, and recent/full cooldown exposure is consistent with Technical Specification §7.
- Note: end-to-end integration of adapter output into the atomic transaction and fetch-history lifecycle (TI-065) depends on the E08 atomic pipeline, which is a separate change; E06 delivers the adapter and its validated provider-record output ready for that pipeline.

## Capabilities

### New Capabilities
- `threatfox-client`: authenticated ThreatFox HTTP client with mode-based URL construction, secret redaction, streaming, timeouts, and cancellation; no independent retry.
- `threatfox-recent-ingestion`: streaming parse of the ThreatFox recent CSV export into normalized provider records for domains, URLs, and `ip:port`.
- `threatfox-full-ingestion`: streaming, memory-bounded decompression and parse of the ThreatFox full `.csv.zip` export with archive-structure validation and archive-bomb guards.
- `threatfox-metadata-mapping`: mapping of ThreatFox fields (native ID, details link, timestamps, threat/malware, confidence, booleans, reporter, tags, port) into the canonical provider record, preserving `false`/`0`/empty values.
- `threatfox-provider-validation`: the ThreatFox-specific provider validator implementing the E03 provider-specific validation extension point.

### Modified Capabilities
- None — `openspec/specs/` is empty; all capabilities are new.

## Impact

- `src/ThreatIntelligence.Application` — consume the canonical provider-record contract and provider-validator extension defined by E03; add the ThreatFox validator and any adapter-facing abstractions.
- `src/ThreatIntelligence.Infrastructure` — new `ThreatFox` provider client and streaming parsers (CSV and zipped CSV); register with `IHttpClientFactory`; reuse `ThreatFoxOptions` (ApiKey) and the seeded provider base URL.
- `src/ThreatIntelligence.Worker` — no direct change in E06; adapter output is consumed by the E05/E08 pipeline (TI-065 deferred to that integration).
- `src/ThreatIntelligence.API` — no endpoint changes.
- `tests/ThreatIntelligence.UnitTests` / `tests/ThreatIntelligence.IntegrationTests` — canned ThreatFox recent/full fixtures, mocked HTTP/streams, archive-bomb/truncated-input guards, and metadata-mapping golden tests (TI-130 fixtures feed this).
- New dependencies: a CSV parser and a zip streaming/decompression approach (provenance recorded in design). No new public API or message-contract changes.
- `.env.example` — `THREATFOX__APIKEY` already present; no secret is ever logged or read back.
