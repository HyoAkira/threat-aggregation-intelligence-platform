## 1. Normalization contracts

- [ ] 1.1 Define the successful normalization result and verify contract tests cover type, value, port, URL host, and parent domain fields (TI-030-TI-035)
- [ ] 1.2 Define stable normalization error codes and verify every public failure code has a non-secret message (TI-030-TI-035)

## 2. IPv4 normalization

- [ ] 2.1 Implement canonical bare IPv4 normalization and verify equivalent valid inputs return dotted-decimal values with NULL port (TI-030)
- [ ] 2.2 Reject ambiguous or malformed IPv4 text and verify table-driven tests cover leading zeros, missing octets, extra octets, signs, and internal whitespace (TI-030)
- [ ] 2.3 Parse IPv4 endpoint ports and verify tests accept 1/65535 and reject 0/65536, signs, whitespace, and non-decimal text (TI-030)

## 3. IPv6 normalization

- [ ] 3.1 Implement lowercase compressed bare IPv6 normalization and verify expanded and compressed equivalents return the same value with NULL port (TI-031)
- [ ] 3.2 Parse bracketed IPv6 endpoint ports and verify valid `[addr]:port` inputs return the address and port separately (TI-031)
- [ ] 3.3 Reject malformed IPv6 endpoints and verify tests cover unmatched brackets, unbracketed port attempts, invalid addresses, and invalid port boundaries (TI-031)

## 4. Public Suffix List resolution

- [ ] 4.1 Add and pin `Nager.PublicSuffix` 3.8.0 and verify dependency restore succeeds (TI-033)
- [ ] 4.2 Add the checked-in PSL snapshot and verify its source URL, retrieval date, and SHA-256 checksum are recorded beside it (TI-033)
- [ ] 4.3 Implement registrable-domain resolution with `LocalFileRuleProvider` and verify fixed tests cover apex, subdomain, wildcard, exception, and multi-level suffix rules (TI-033)
- [ ] 4.4 Implement PSL fallback behavior and verify tests cover IP literals, single-label hosts, unknown suffixes under the prevailing rule, and public-suffix-only inputs (TI-033)
- [ ] 4.5 Verify PSL resolution works without network access and does not attempt a startup refresh (TI-033)

## 5. Domain normalization

- [ ] 5.1 Implement domain trimming, invariant lowercase conversion, and removal of exactly one trailing root dot, and verify canonical examples (TI-032)
- [ ] 5.2 Validate domain labels and total length and verify tests cover empty labels, invalid characters, hyphen boundaries, 63-character labels, and the 253-character total limit (TI-032)
- [ ] 5.3 Reject non-domain syntax and verify tests cover schemes, paths, ports, internal whitespace, TLD-only values, and multiple trailing dots (TI-032)
- [ ] 5.4 Preserve Unicode IDN form and verify Unicode and punycode inputs remain distinct normalized values (TI-032)
- [ ] 5.5 Attach the registrable parent domain to valid domain results and verify apex and subdomain results use the pinned PSL rules (TI-032, TI-033)

## 6. URL normalization

- [ ] 6.1 Validate absolute URL structure and verify only HTTP/HTTPS URLs with a non-empty valid host are accepted (TI-034)
- [ ] 6.2 Normalize scheme and DNS-host casing and verify path and query casing remain unchanged (TI-034)
- [ ] 6.3 Canonicalize IPv4 and IPv6 URL hosts and verify the same strict address rules are reused (TI-034)
- [ ] 6.4 Remove URL fragments and verify every other preserved URL component remains unchanged (TI-034)
- [ ] 6.5 Remove an empty root slash and verify non-root paths, including trailing slashes, remain unchanged (TI-034)
- [ ] 6.6 Normalize explicit default ports and verify they are removed from `iocValue` but returned in `port` (TI-034)
- [ ] 6.7 Normalize explicit non-default ports and verify they remain in `iocValue` and are returned in `port` (TI-034)
- [ ] 6.8 Preserve path/query delimiters and percent-encoded octets and verify the normalizer neither decodes nor re-encodes them (TI-034)
- [ ] 6.9 Enforce the normalized 2048-character URL limit and verify exact-boundary and over-boundary cases (TI-034)
- [ ] 6.10 Attach lowercase `urlHost` and its registrable `parentDomain` and verify subdomain, Unicode, IP-literal, and localhost-like hosts (TI-034, TI-033)

## 7. Query classification

- [ ] 7.1 Classify strict IPv4 before other types and verify bare and port-bearing inputs return Ipv4 results (TI-035)
- [ ] 7.2 Classify strict IPv6 before other types and verify bare and bracketed-port inputs return Ipv6 results (TI-035)
- [ ] 7.3 Classify any input containing `://` as a URL attempt and verify malformed or unsupported schemes return URL-specific errors without domain fallback (TI-035)
- [ ] 7.4 Classify remaining valid domains and verify invalid residual inputs return stable `InvalidInput` failures (TI-035)
- [ ] 7.5 Verify classifier outputs are application-level results with no HTTP dependency; E09 remains responsible for RFC 7807 mapping (TI-035)

## 8. Canonical provider records

- [ ] 8.1 Define the canonical provider-record input contract and verify it exposes every common field and an opaque provider payload (TI-036)
- [ ] 8.2 Define the normalized provider-record output contract and verify it cannot represent an unnormalized IOC identity (TI-036)
- [ ] 8.3 Define the structured validation-failure contract and verify record index, provider identity, code, field, and message length rules (TI-036)

## 9. Common provider-record validation

- [ ] 9.1 Validate provider identity and verify non-positive provider IDs and blank/overlength provider-record IDs fail first (TI-036)
- [ ] 9.2 Validate raw IOC presence and verify blank values fail before normalization (TI-036)
- [ ] 9.3 Normalize the IOC and verify a declared type mismatch returns the stable type-mismatch error (TI-036)
- [ ] 9.4 Validate provider timestamps and verify UTC values pass while local/unspecified values fail (TI-036)
- [ ] 9.5 Validate the optional provider-details link and verify scheme, structure, and 2048-character boundary cases (TI-036)
- [ ] 9.6 Normalize tag collections and verify trimming, lowercase conversion, blank removal, de-duplication, and length limits (TI-036)

## 10. Provider-specific validation extension

- [ ] 10.1 Define the provider-validator extension contract and verify one validator is selected by provider ID (TI-036)
- [ ] 10.2 Invoke the registered validator after common validation and verify its failure is returned for the current record (TI-036)
- [ ] 10.3 Fail closed when no provider validator is registered and verify the stable unsupported-provider error (TI-036)

## 11. Ordering and secret safety

- [ ] 11.1 Stop at the first invalid input record and verify a later failure is not evaluated or returned (TI-036)
- [ ] 11.2 Apply the documented within-record field order and verify multi-error input always returns the same first failure (TI-036)
- [ ] 11.3 Sanitize validation failures and verify raw IOC, opaque payload, credentials, headers, and credential-bearing URLs never appear in errors or logs (TI-036)
- [ ] 11.4 Keep validation side-effect free and verify failures write no IOC, tag, provider, or fetch-history data (TI-036)

## 12. Change verification

- [ ] 12.1 Run `dotnet build` and the complete unit test suite and verify all E03 tests pass
