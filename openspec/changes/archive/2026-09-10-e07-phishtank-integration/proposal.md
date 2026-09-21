## Why

The PRD (Data Sources & Ingestion Model, FR-007, US-008/US-009) requires PhishTank as the second public provider, ingested as a full-dump `online-valid.json.gz` feed on its configured interval. The seeded default interval is 8 hours. No PhishTank HTTP client, streaming parser, or record mapping exists, so PhishTank data cannot be collected. The E05 change supplies the messaging/guard/history shell and the `IProviderFetchExecutor` seam; this epic fills that seam with the PhishTank adapter.

## What Changes

- Add a PhishTank HTTP client: no required app key, a descriptive user agent, safe timeouts, streaming of `online-valid.json.gz`, optional future app-key support, and no logging of any configured key.
- Add a streaming full-feed JSON parser that never buffers the whole file, distinguishes transport / decompression / JSON / record-validation failures, and stays within a documented memory bound (≤256 MiB on a fixture at least twice the sampled full-feed size, recalibrated with recorded evidence).
- Map every applicable record field: `phish_id`/`phish_detail_url` (provider record ID and details link), normalized URL, `submission_time` → `firstSeenUtc`, `verification_time` → `lastSeenUtc`, `verified`, `online`, `target`, and a complete validated `phishTankDetails` JSON preserving every network detail object. Contextual detail IPs remain JSON, never standalone IOC rows.
- Make PhishTank 429/rate-limit responses fail immediately without retry, recording a clear operational reason.
- Integrate the adapter end-to-end through the E05 lifecycle and E08 atomic pipeline: a valid fixture feed commits once with correct counters; any bad record (including a bad final record) rolls back the entire batch with a Failed history row and zero counters.

## Capabilities

### New Capabilities
- `phishtank-client`: the PhishTank HTTP client — user agent, streaming download, timeouts/cancellation, optional app-key handling, and quota signaling.
- `phishtank-ingestion`: streaming full-feed parsing with a memory bound and error taxonomy, record mapping, `phishTankDetails` preservation, and end-to-end atomic-pipeline behavior.
- `phishtank-quota-errors`: PhishTank 429 immediate-failure behavior.

### Modified Capabilities
- None — `openspec/specs/` is empty. General trigger guards and non-retryable error classification are specified by the E05 change and are not altered here; this change adds PhishTank adapter behavior and quota error handling.

## Impact

- `src/ThreatIntelligence.Worker` — the PhishTank adapter implementing `IProviderFetchExecutor` (the E05 seam), plus its HTTP client and streaming parser.
- `src/ThreatIntelligence.Application` — provider-record and executor abstractions consumed by the adapter.
- `src/ThreatIntelligence.Infrastructure` — the PhishTank client and parser; no schema or config changes.
- Dependencies: E03 normalization (URL normalization, `urlHost`/`parentDomain`), E05 messaging/lifecycle shell, E08 atomic upsert pipeline (TI-075). No new `.env` keys.
- `tests/ThreatIntelligence.UnitTests` — streaming parse, mapping, and memory-bound tests.
- `tests/ThreatIntelligence.IntegrationTests` — simulated 429 and end-to-end fixture commit/rollback against real persistence and broker.
