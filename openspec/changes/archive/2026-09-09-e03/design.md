## Context

See proposal.md - Why. E03 adds a shared normalization + validation layer consumed by both the API search path (E09) and the worker ingestion pipeline (E06/E07). No normalization code exists yet; the technical contract is Technical Specification §5.

## Goals / Non-Goals

**Goals:**
- One reusable normalization contract that E06-E09 can consume without host-specific behavior.
- Table-driven test coverage per rule in §5, including negative and boundary cases.
- Deterministic provider-record validation order and a safe structured failure contract.
- Deterministic, provider-scoped identity compatible with E08 hashing and E09 grouping.

**Non-Goals:**
- No API or message-contract changes.
- No persistence/schema changes (identity hashing already covered by E02).
- No provider-specific adapter logic (that belongs to E06/E07).
- No API or Worker flow wiring; those paths do not exist until E06-E09.

## Decisions

- **Service placement**: normalization, classification, canonical provider-record types, and common validation live in Application. PSL resolution is an Application abstraction implemented in Infrastructure. API and Worker register the same services later. This prevents host drift without putting application policy in Domain.
- **Typed results instead of HTTP concerns**: the classifier returns either a normalized identity (`IocType`, `IocValue`, nullable `Port`, nullable `UrlHost`, nullable `ParentDomain`) or a stable error code plus safe message. It does not return status codes. E09 maps any classification failure to RFC 7807 HTTP 400.
- **Public Suffix List implementation**: use `Nager.PublicSuffix` 3.8.0 with `LocalFileRuleProvider` and a checked-in `public_suffix_list.dat` snapshot obtained from publicsuffix.org. Record the snapshot source URL, retrieval date, and SHA-256 checksum beside the data file. MVP performs no startup download or background refresh. Updating the snapshot is an explicit reviewed maintenance change; tests use fixed examples and MUST NOT depend on the machine or network.
- **PSL fallback semantics**: IP literals and single-label/localhost-like hosts return themselves. Names with an unlisted suffix follow the PSL prevailing `*` rule, so `a.example.unknown` resolves to `example.unknown`; a public suffix alone has no registrable domain.
- **Normalization result**: every normalizer returns the canonical IOC value, exact `IocType`, explicit input port when present, and relation fields where applicable. Default URL ports are removed from `IocValue` but remain in `Port`; non-default ports remain in both.
- **URL preservation boundary**: only outer whitespace, scheme/host casing, IP host spelling, default-port text, fragment, and an empty root slash may change. Path/query case, order, delimiters, and percent-encoded octets are not decoded or re-encoded.
- **Auto-classification precedence**: after trimming, try strict bare/port-bearing IPv4 and IPv6, then treat any `://` marker as a URL attempt, then try domain. An unsupported or malformed URL marker is a URL validation failure and never falls through to domain.
- **Canonical provider record**: define provider ID, provider-record ID, raw IOC input, optional declared IOC type, provider timestamps, optional provider-details link, common optional fields, tags, and opaque provider payload. Provider adapters construct this record; the validator produces a normalized validated record for E08.
- **Ordered validation**: evaluate records in input order and stop on the first invalid record. Within a record validate identity, raw IOC presence, normalization/type agreement, timestamps, details link, tags, then the registered provider-specific validator. A failure contains one-based record index, provider ID, truncated provider-record ID, stable code, field, and safe message; it never contains raw IOC, opaque payload, credentials, headers, or URLs containing credentials.
- **Provider extension point**: E03 defines the provider-validator interface and rejects a record when a required provider validator is absent. E06/E07 implement their field and payload rules. E08 owns whole-batch rollback and history updates.

## Risks / Trade-offs

- [The checked-in PSL snapshot becomes stale] → Replace it through a deliberate reviewed update, record its new retrieval date and SHA-256 checksum, and rerun fixed regression tests; runtime behavior remains reproducible and offline.
- [Strictness rejects valid-looking but ambiguous input] → Mitigation: descriptive errors and table-driven tests align with §5 examples.
- [Provider rules are delivered after the common pipeline] → Missing registered provider validators fail closed, and E06/E07 cannot declare adapter completion until their validator is registered.
