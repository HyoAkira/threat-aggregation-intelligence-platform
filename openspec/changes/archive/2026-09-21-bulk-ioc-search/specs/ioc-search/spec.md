## MODIFIED Requirements

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
