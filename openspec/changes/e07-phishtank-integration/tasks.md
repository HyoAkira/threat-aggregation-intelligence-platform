## 1. PhishTank HTTP client

- [ ] 1.1 Register a named `PhishTank` HTTP client (descriptive user agent, explicit timeout) via `IHttpClientFactory` and verify a mocked request carries the user agent
- [ ] 1.2 Implement streaming download with `ResponseHeadersRead` + generic `Content-Encoding` decompression (gzip/identity) and verify the body is streamed without whole-file buffering
- [ ] 1.3 Add optional app-key support (default unconfigured, never logged) and verify a configured key appears nowhere in logs or URLs and that no key still works
- [ ] 1.4 Surface HTTP 429 as a typed quota condition and other non-success as transport errors; verify a simulated 429 raises the quota type and a 5xx raises transport

## 2. Streaming parse and memory bound

- [ ] 2.1 Implement streaming full-feed parsing via `JsonSerializer.DeserializeAsyncEnumerable<T>` over the decompressed stream and verify records are produced one at a time without whole-file retention
- [ ] 2.2 Implement the failure taxonomy (transport / decompression / JSON / record-validation) and verify corrupt and truncated gzip/JSON inputs fail cleanly with distinct error classes
- [ ] 2.3 Capture the real sampled full-feed size from a dev fetch, record it in the test fixture notes, and add a memory test on a fixture ≥2× that size asserting ≤256 MiB additional resident memory; recalibrate the threshold with the recorded evidence if needed

## 3. Record mapping and details preservation

- [ ] 3.1 Map record fields to the provider record (`phish_id` → providerRecordId, `phish_detail_url` → providerDetailsLink, URL through the shared normalization service, `submission_time` → firstSeenUtc, `verification_time` → lastSeenUtc, `verified`/`online`/`target`) and verify a golden record retains every value including false/zero/empty
- [ ] 3.2 Serialize `phishTankDetails` from the raw detail array and verify a round-trip test preserves every field (`ipAddress`, `cidrBlock`, `announcingNetwork`, `rir`, `detailTime`) and that the stored value passes the `ISJSON` constraint
- [ ] 3.3 Verify contextual detail IPs never become standalone IOC rows (test asserts no IP-type provider records are produced from details)

## 4. Rolling budget exposure

- [ ] 4.1 Add a history query counting logical fetch starts in a rolling UTC window and implement `fetchesPast24h` for the PhishTank provider, excluding internal retries
- [ ] 4.2 Add window-boundary unit tests (exactly at the edge, three-in-window blocked with 409, allowed after the oldest start leaves the window, retries not counted) and verify they pass
- [ ] 4.3 Expose `fetchesPast24h` on the provider dashboard payload and verify the admin provider response includes it

## 5. Quota behavior

- [ ] 5.1 Verify end-to-end that a simulated 429 results in exactly one attempt, a Failed history row with the quota reason, and zero IOC writes (integration test)

## 6. Adapter integration with the atomic pipeline

- [ ] 6.1 Implement the PhishTank adapter as `IProviderFetchExecutor` (full mode only) returning mapped provider records or throwing the typed taxonomy exceptions, and register it in Worker DI
- [ ] 6.2 Verify the consumer invokes the adapter through the E05 lifecycle shell and a valid sanitized fixture commits exactly once with correct added/updated counters
- [ ] 6.3 Verify a bad final record rolls back the entire IOC batch with a Failed history row, zero counters, and the offender named (no partial writes)

## 7. Fixtures and verification

- [ ] 7.1 Add a sanitized `online-valid.json.gz` fixture covering all record shapes (null/false/zero values, detail arrays, malformed record, truncated variant) with a fake HTTP handler for tests
- [ ] 7.2 Verify the clean build, unit tests, and integration tests pass and document the feed-size evidence and memory-bound result in the operator/README notes