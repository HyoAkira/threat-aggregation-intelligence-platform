## Context

See proposal.md - Why. E06 delivers the ThreatFox adapter: an authenticated HTTP client plus streaming parsers that turn ThreatFox recent/full exports into the canonical provider records defined by E03's `provider-record-validation` spec. The domain model (IoC entity, `ThreatFoxOptions.ApiKey`, provider seed id 1) and the E03 normalization/validation layer already exist or are in flight. The worker messaging lifecycle (E05) and the atomic upsert pipeline (E08) are not yet implemented, so E06 scopes to adapter output ready for that pipeline; TI-065 integration is deferred to E08.

## Goals / Non-Goals

**Goals:**
- A secret-safe ThreatFox HTTP client supporting both export modes with streaming, timeout, and cancellation, and no client-level retry.
- Streaming CSV parse (recent) and streaming zipped-CSV parse (full) producing normalized provider records, with bounded memory.
- Full fidelity mapping of ThreatFox fields into the canonical provider record, preserving `false`/`0`/empty values.
- A ThreatFox provider validator registered on the E03 extension point.
- Table-driven unit tests using canned fixtures plus archive-bomb/truncated/corrupt guards; mocked HTTP proves URL/mode selection and secret redaction.

**Non-Goals:**
- No database writes, no fetch-history updates (E08 owns the transaction/lifecycle; E03 validation is side-effect free).
- No API endpoints (E09) and no message-contract changes.
- No scheduler, guard, cooldown, or MassTransit retry wiring (E05).
- No PhishTank adapter (E07).
- No configuration of a new secret: `THREATFOX__APIKEY` and provider base URL already exist.

## Decisions

- **Adapter placement**: ThreatFox client and parsers live in Infrastructure under a `ThreatFox` namespace, registered through `AddInfrastructureServices`. The client is an Application abstraction (`IProviderClient`-style) implemented in Infrastructure; parsers produce the E03 canonical provider record. Rationale: keeps HTTP/streaming concerns out of Application and Domain, matching the existing Infrastructure/Application split. Alternative (putting parsing in Application) rejected because streaming and zip decompression are infrastructure concerns.
- **HTTP client via `IHttpClientFactory`**: register a typed client with an explicit timeout and no handler-level retry (E05 owns retries). Rationale: standard, testable via mocked `HttpMessageHandler`; avoids a hand-built `HttpClient`. Base URL comes from the seeded provider row, key from `ThreatFoxOptions`.
- **CSV parsing**: use `CsvHelper` (a mature streaming CSV reader) for both recent CSV and the full export's CSV entry. Rationale: correct quoted-field/escaping handling and row-by-row streaming without custom parsing bugs. Alternative (hand-rolled split) rejected for escaping edge cases.
- **Zip streaming**: use `System.IO.Compression.ZipArchive` over the response stream, locate the expected CSV entry by name, and read it as a stream; enforce an uncompressed-size/entry guard before full decompression to bound memory and stop archive bombs. Rationale: streaming via ZipArchive uses the underlying stream rather than buffering the whole archive.
- **Mode-to-URL mapping**: `Recent` → `domains_recent.csv` + `urls_recent.csv` + `ip_port_recent.csv`; `Full` → `full.csv.zip`. The mapping is centralized so mode selection is testable and the key-bearing URL is never logged. Rationale: mirrors Technical Specification §8 endpoints; keeps the key out of diagnostics.
- **Mapping**: a pure mapper converts parsed ThreatFox columns to the canonical provider record, applying nullable vs. `false`/`0`/empty distinctions and normalizing tags. Rationale: isolates mapping policy for golden tests.
- **Provider validator**: register a ThreatFox validator on the E03 provider-specific extension point keyed by provider ID; missing/unregistered validator fails closed. Rationale: E03 contract already requires one registered validator per provider.

## Risks / Trade-offs

- [Key could leak via URL if a diagnostic logs the full request] → Centralized URL construction, explicit redaction rule (never log the request URL or key), and a unit test asserting absence from logs/exceptions.
- [Zip archive bomb exhausts memory] → Enforce uncompressed-size/entry limits before full decompression and bound the memory target; archive-bomb/truncated/corrupt tests cover it.
- [Streaming CSV row with unexpected columns or encoding] → Validate expected headers/column count per row and report a structured row error; tests cover malformed rows.
- [ThreatFox schema drift over time] → Column mapping is isolated and fixture-golden; drift is caught by mapping tests and updated deliberately.
- [Adapter output not yet wired to a transaction] → E06 explicitly defers TI-065 to E08; the adapter contract is the E03 provider record, so E08 can consume it unchanged.

## Migration Plan

- No schema, API, or message-contract changes. Adding the ThreatFox client/parsers and a provider validator is additive; registering services in Infrastructure is backward compatible. No rollback beyond reverting the additive E06 changes.

## Open Questions

- None that would change the specs, approach, or task breakdown.
