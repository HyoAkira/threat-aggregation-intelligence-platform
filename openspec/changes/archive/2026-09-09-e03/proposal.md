## Why

Ingestion (E06/E07 providers) and user query input (E09 search) both need one deterministic, provider-scoped identity: the Technical Specification §5 defines strict normalization rules for IPv4, IPv6, domain, and URL plus a query auto-classification precedence, but no shared normalization service exists yet. Without it, the same indicator could be stored and searched differently across paths, breaking the deduplication and grouped-attribution guarantees that E08 and E09 rely on.

## What Changes

- Add a shared normalization service used by both ingestion and query paths, covering strict IPv4, strict IPv6, domain, and URL normalization per Technical Specification §5.
- Add query type auto-classification (`IP → URL → domain` precedence) that returns a typed normalization failure which E09 maps to HTTP 400.
- Integrate deterministic Public Suffix List resolution to calculate `parentDomain`, using `Nager.PublicSuffix` 3.8.0 with a checked-in, checksummed PSL snapshot and no runtime network dependency.
- Add a canonical provider-record contract and an ordered common-validation pipeline. The pipeline exposes a provider-specific validator extension point; E06 and E07 supply the concrete ThreatFox and PhishTank rules.

## Capabilities

### New Capabilities
- `ioc-normalization`: shared strict normalization for IPv4, IPv6, domain, and URL, plus query type auto-classification, per Technical Specification §5.
- `public-suffix-list`: registrable-domain (`parentDomain`) resolution for domain and URL hosts, with a defined data-update strategy.
- `provider-record-validation`: canonical incoming-record contract, ordered common validation, safe error shape, and provider-specific validation extension point.

### Modified Capabilities
- None — `openspec/specs/` is empty; all capabilities are new.

## Impact

- `src/ThreatIntelligence.Domain` — no entity changes expected; may add normalization result/value types if needed.
- `src/ThreatIntelligence.Application` — new normalization and validation services under an application-level abstraction consumed by both query handlers and the worker ingestion pipeline.
- `src/ThreatIntelligence.Infrastructure` — Public Suffix List data access/update strategy.
- `src/ThreatIntelligence.API` — no endpoint change in E03; E09 later consumes the classifier and maps typed failures to ProblemDetails.
- `src/ThreatIntelligence.Worker` — no fetch-flow change in E03; E06/E07 later add provider validators and E08 invokes the pipeline before atomic upsert.
- `tests/ThreatIntelligence.UnitTests` — table-driven tests per rule in §5, including negative/boundary/whitespace/IP-bracket cases.
- New dependency: a Public Suffix List library/data source (provenance recorded in design); no other external dependencies expected.
- No public API contract or message-contract changes; E03 is a foundation epic consumed by E06–E09.
