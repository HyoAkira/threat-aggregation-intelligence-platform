# ioc-search Specification

## Purpose

Defines the combined IOC search endpoint: how a free-form query is auto-classified and normalized, how exact and domain-relation matches are grouped across providers and ports, and how optional tag and provider filters narrow matched groups' sources.

## Requirements

### Requirement: Query auto-classification and normalization
`GET /api/iocs/search` SHALL require a non-empty `q` parameter. After trimming surrounding whitespace, the query SHALL be classified and normalized using the shared normalization rules: a strict bare or port-bearing IPv4 or IPv6 parse yields the IP type; an input starting with `http(s)://` or containing `://` yields the URL type; otherwise a valid DNS name yields the domain type. Any other input, an empty or whitespace-only query, a URL shorter than its scheme, or an input that only resembles an IOC but fails normalization SHALL be rejected with a 400 response whose detail names the reason without echoing secrets. Normalization is applied identically whether the input arrives through this endpoint, the bulk-search endpoint, or ingestion.

#### Scenario: Bare IPv4 with leading zeros is rejected
- **WHEN** the query is `1.2.3.04`
- **THEN** the endpoint returns a 400 invalid-input response describing the ambiguous IPv4 form

#### Scenario: Scheme-bearing input classifies as URL
- **WHEN** the query is `HTTP://Example.COM:443/a`
- **THEN** the value is normalized to a canonical URL whose host is lowercased and whose default port is dropped, and the search runs against the normalized URL

#### Scenario: Unclassifiable input is rejected
- **WHEN** the query is `not an ioc at all!`
- **THEN** the endpoint returns a 400 invalid-input response

#### Scenario: Bulk input uses the same classifier
- **WHEN** a bulk request includes a value that is valid under the single-search rules
- **THEN** that value is normalized to the same IOC type and canonical value before matching

### Requirement: Grouped indicator and domain-relation matches
The endpoint SHALL return a paged group envelope. An IP or URL query SHALL search for rows whose normalized `(iocType, iocValue)` equals the normalized query. A domain query SHALL include an exact domain IOC plus every IOC whose `urlHost` or `parentDomain` equals the normalized domain. Each returned group SHALL contain every matching provider row. A bare IP query SHALL group matching rows across all explicit ports; a port-bearing IP query (`1.2.3.4:443` or `[2001:db8::1]:443`) SHALL match only rows with that exact address and explicit port. Exact identity matching SHALL be safe against hash collision: the stored normalized value is compared after the value-hash lookup before a row counts as a match.

#### Scenario: URL host finds stored URL IOCs
- **WHEN** the query is `shop.example.co.uk` and stored URL IOCs have `urlHost=shop.example.co.uk`
- **THEN** the response contains those URL IOC groups even when no exact domain IOC is stored

#### Scenario: Parent domain finds subdomain and URL IOCs
- **WHEN** the query is `example.co.uk` and stored domain or URL IOCs have `parentDomain=example.co.uk`
- **THEN** the response contains every matching IOC group

#### Scenario: Bare IP groups port variants and providers
- **WHEN** the query is `1.2.3.4` and rows exist for `1.2.3.4` (no port) from ThreatFox, `1.2.3.4:443` from ThreatFox, and `1.2.3.4` from PhishTank
- **THEN** one group is present in the response whose sources include all three rows

#### Scenario: Port-bearing IP matches only that port
- **WHEN** the query is `1.2.3.4:443`
- **THEN** the matching group contains only rows for address 1.2.3.4 with explicit port 443

#### Scenario: Equivalent IPv6 forms match one normalized identity
- **WHEN** the query is `2001:0db8:0:0:0:0:0:1` and a row stores compressed `2001:db8::1`
- **THEN** the query matches that row after normalization

### Requirement: Combined tag and provider filters narrow sources
When a request supplies the optional `tag` and/or `provider` parameter alongside `q`, the endpoint SHALL narrow every candidate group's `sources` to rows that carry the normalized tag (trimmed and lowercased) and/or belong to the identified provider. Group-level aggregate first/last seen values SHALL be recomputed over the returned sources. Groups with no matching sources SHALL be omitted. If no group exists for the query, or if the filters leave no matching group, the endpoint SHALL return 404 not-found. The same filter normalization and source attribution SHALL apply independently to every query in a bulk-search request.

#### Scenario: Tag filter keeps only sources carrying the tag
- **WHEN** the query is `1.2.3.4` with `tag=botnet` and only one of the matching rows carries tag `botnet`
- **THEN** the group is returned with a single source (the tagged row), and that source's tag list identifies it as the supplier

#### Scenario: Provider filter narrows to one provider
- **WHEN** the query matches a ThreatFox row and a PhishTank row with `provider=1`
- **THEN** the returned group's sources contain only the ThreatFox row

#### Scenario: Filter that removes every source returns 404
- **WHEN** the query matches rows but none carries the requested tag
- **THEN** the endpoint returns a 404 not-found response rather than an empty group

#### Scenario: Tag value is normalized before matching
- **WHEN** the request uses `tag= BotNet `
- **THEN** it matches sources whose stored tag name is `botnet`

#### Scenario: Bulk filters are evaluated per value
- **WHEN** a bulk request uses a provider filter and only one submitted value has a source from that provider
- **THEN** that value is matched and the other valid values are reported as not found when the filter eliminates their sources

### Requirement: Not-found and invalid-input distinction
A valid normalized query that has no matching group SHALL return 404 with a not-found error identifying the searched indicator type and value. A syntactically invalid or unclassifiable query SHALL return 400. Both responses SHALL use the shared ProblemDetails shape and SHALL NOT be confused with one another.

#### Scenario: Valid but absent indicator returns 404
- **WHEN** the query normalizes successfully but no stored row matches
- **THEN** the endpoint returns 404 with a not-found problem detail for that indicator

### Requirement: Malware and target filters narrow sources
When a request supplies the optional `malware` and/or `target` parameter alongside `q`, the endpoint SHALL narrow every candidate group's `sources` to rows whose stored `malware` or `target` value equals the supplied value after trimming, compared case-insensitively. These filters SHALL compose with each other and with the existing `tag` and `provider` filters. A blank or whitespace-only value SHALL behave as if the parameter were omitted; after trimming, malware SHALL contain no more than 200 characters and target no more than 500 characters, otherwise the endpoint SHALL return 400 invalid-input. Group-level aggregate first/last seen values SHALL be recomputed over the returned sources. Groups with no matching sources SHALL be omitted. If no group exists for the query, or if the filters leave no matching group, the endpoint SHALL return 404 not-found.

#### Scenario: Target filter keeps only targeting sources
- **WHEN** the query matches a group with one source whose stored `target` is `Amazon` and one source with a different target
- **THEN** the group is returned with only the Amazon source

#### Scenario: Malware filter narrows to the family
- **WHEN** the query matches rows whose stored `malware` is `Emotet` and rows with other malware values
- **THEN** only the rows whose stored `malware` matches the requested value remain in the group

#### Scenario: Malware and target filters compose with provider
- **WHEN** the request supplies `malware=emotet` and `provider=1` and only a ThreatFox row satisfies both
- **THEN** the returned group's sources contain only the matching row

#### Scenario: Filter that removes every source returns 404
- **WHEN** the query matches rows but none has the requested malware or target value
- **THEN** the endpoint returns a 404 not-found response rather than an empty group

#### Scenario: Blank filter is ignored
- **WHEN** the request uses `target=` or `target=   `
- **THEN** the response is the same as if `target` were omitted

#### Scenario: Oversized filter is rejected
- **WHEN** the request supplies a trimmed malware value over 200 characters or a target value over 500 characters
- **THEN** the endpoint returns a 400 invalid-input problem detail
