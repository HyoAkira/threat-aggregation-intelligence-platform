## 1. ThreatFox HTTP client

- [ ] 1.1 Add the Application-level provider-client abstraction (used by the adapter) and verify it compiles against the E03 canonical provider-record contract (TI-060)
- [ ] 1.2 Implement the authenticated ThreatFox client in Infrastructure that builds recent and full export URLs from the configured base URL and `ThreatFoxOptions.ApiKey` without logging the key, and verify mocked requests prove recent/full mode-to-URL selection (TI-060)
- [ ] 1.3 Register the ThreatFox client with `IHttpClientFactory` and an explicit timeout and no handler-level retry, and verify DI registration resolves and the timeout is applied (TI-060)
- [ ] 1.4 Set a descriptive user agent, stream the response, and honor a cancellation token, and verify a unit test asserts the user agent, streaming, and cancellation behavior (TI-060)
- [ ] 1.5 Verify secret redaction: a test asserts the API key and the credential-bearing URL never appear in logs or exception messages (TI-060)

## 2. Recent-export ingestion

- [ ] 2.1 Add `CsvHelper` and verify dependency restore succeeds (TI-061)
- [ ] 2.2 Implement streaming parse of the recent CSV (domains, URLs, `ip:port`) into normalized provider records using the E03 normalization service, and verify a canned fixture maps every supported IOC type (TI-061)
- [ ] 2.3 Verify recent rows stream row-by-row without buffering the whole feed, and verify a test asserts bounded memory over a fixture (TI-061)
- [ ] 2.4 Reject a malformed recent row with a structured failure naming the row and cause, and verify table-driven tests cover malformed rows (TI-061)

## 3. Full-export ingestion (zipped)

- [ ] 3.1 Implement streaming decompression of the `.csv.zip` archive via `System.IO.Compression.ZipArchive` over the response stream, locate the expected CSV entry, and parse it into normalized provider records, and verify a fixture archive is parsed (TI-062)
- [ ] 3.2 Enforce an uncompressed-size/entry guard before full decompression and verify archive-bomb tests reject it (TI-062)
- [ ] 3.3 Verify corrupt or truncated archives fail cleanly with a structured decompression error, and verify tests cover corrupt/truncated input (TI-062)
- [ ] 3.4 Verify bounded memory on a full export at least twice the sampled feed size and verify a test records the memory bound (TI-062)

## 4. Metadata and tag mapping

- [ ] 4.1 Implement the ThreatFox-to-canonical-provider-record mapper covering native ID, details link, timestamps, threat/malware fields, confidence, booleans, reporter, tags, and port, and verify a golden provider row retains every applicable value (TI-063)
- [ ] 4.2 Verify `false`, `0`, and empty values are retained rather than coerced to null while absent values remain null, and verify table-driven tests cover each (TI-063)
- [ ] 4.3 Verify tag normalization (trim, lowercase, de-duplicate) and separate port storage, and verify tests cover tag and `ip:port` cases (TI-063)

## 5. ThreatFox provider validation

- [ ] 5.1 Implement the ThreatFox provider validator on the E03 provider-specific extension point keyed by provider ID, and verify a valid ThreatFox record is accepted (TI-036, TI-065 dependency)
- [ ] 5.2 Verify a non-ThreatFox record fails closed with the stable unsupported-provider error, and verify the test covers it (TI-036, TI-065 dependency)
- [ ] 5.3 Verify ThreatFox validation failures conform to the E03 safe failure contract (record index, provider ID, truncated record ID, code, field, safe message) with no raw IOC, payload, credentials, or credential-bearing URL, and verify the test covers it (TI-036, TI-065 dependency)

## 6. Cadence and failure behavior verification

- [ ] 6.1 Verify the adapter surfaces upstream 429/5xx/timeout/cancellation without client-level retry, and verify mocked HTTP tests exercise each classification at the adapter boundary (TI-064)
- [ ] 6.2 Verify recent/full export mode availability is exposed consistently with Technical Specification §7 (ThreatFox exposes Recent and Full; recent 5-minute and full 60-minute cooldowns are enforced by E05), and verify the mode/cadence surface is consistent (TI-064)
- [ ] 6.3 Verify end-to-end fixture ingestion reaches the documented adapter boundary (records produced, no DB writes), ready for E08 integration (TI-065 dependency)

## 7. Change verification

- [ ] 7.1 Run `dotnet build` and the complete unit/integration test suite and verify all E06 tests pass
- [ ] 7.2 Confirm no secret is logged and `THREATFOX__APIKEY` remains the only ThreatFox secret (no new config), and verify the redaction test passes
