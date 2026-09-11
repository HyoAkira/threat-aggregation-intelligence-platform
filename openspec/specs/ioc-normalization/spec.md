# ioc-normalization Specification

## Purpose

Provides one shared, deterministic normalization service for IPv4, IPv6, domain, and URL indicators plus query type auto-classification, so ingestion and search paths produce identical provider-scoped identities.

## Requirements

### Requirement: IPv4 normalization
The system SHALL trim outer whitespace, normalize a strict IPv4 address to canonical dotted-decimal form, reject ambiguous inputs such as leading-zero octets, and return an explicit port separately when present. A port SHALL be decimal and between 1 and 65535.

#### Scenario: Canonical IPv4
- **WHEN** a bare IPv4 address is normalized
- **THEN** the canonical dotted-decimal address is returned and the port is NULL

#### Scenario: IPv4 with port
- **WHEN** an IPv4 with a valid port (e.g. `1.2.3.4:443`) is normalized
- **THEN** the canonical address is returned and the port is stored separately

#### Scenario: Leading-zero IPv4 rejected
- **WHEN** an address contains ambiguous leading-zero octets
- **THEN** normalization fails with a descriptive error

#### Scenario: IPv4 port boundaries
- **WHEN** an IPv4 endpoint has port `1` or `65535`
- **THEN** normalization succeeds and returns that port separately

#### Scenario: Invalid IPv4 port rejected
- **WHEN** an IPv4 endpoint has port `0`, `65536`, a sign, whitespace, or non-decimal characters
- **THEN** normalization fails with a descriptive port error

### Requirement: IPv6 normalization
The system SHALL trim outer whitespace, normalize a strict IPv6 address to its lowercase compressed canonical representation, and parse a port only from bracketed `[addr]:port` syntax. A port SHALL be decimal and between 1 and 65535.

#### Scenario: Expanded and compressed forms match
- **WHEN** an expanded IPv6 address and its compressed equivalent are normalized
- **THEN** both produce the identical canonical value

#### Scenario: Bracketed IPv6 with port
- **WHEN** `[2001:db8::1]:443` is normalized
- **THEN** the canonical address is returned and port `443` is stored separately

#### Scenario: Invalid brackets or port
- **WHEN** brackets or a port are malformed
- **THEN** normalization fails with a clear error

#### Scenario: Unbracketed IPv6 is bare
- **WHEN** a valid unbracketed IPv6 address is normalized
- **THEN** its colons are treated as address syntax and the returned port is NULL

### Requirement: Domain normalization
The system SHALL trim outer whitespace, lowercase a domain invariantly, strip exactly one trailing root dot, validate each label and the total normalized length, reject schemes, paths, ports, internal whitespace, empty labels, and public-suffix-only values, and preserve Unicode IDN form without punycode conversion. The normalized domain SHALL be at most 253 characters and every label SHALL be 1-63 characters.

#### Scenario: Valid domain
- **WHEN** a valid domain with mixed case and a trailing dot is normalized
- **THEN** a lowercase form without the trailing root dot is returned

#### Scenario: Invalid domain rejected
- **WHEN** a value contains a scheme, path, port, whitespace, invalid labels, exceeds length limits, or is TLD-only
- **THEN** normalization fails with a descriptive error

#### Scenario: IDN preserved
- **WHEN** a Unicode IDN and its punycode equivalent are normalized
- **THEN** they remain distinct values (no punycode conversion)

#### Scenario: More than one root dot rejected
- **WHEN** a domain ends with two or more dots
- **THEN** removing one root dot leaves an empty label and normalization fails

### Requirement: URL normalization
The system SHALL trim outer whitespace and accept only absolute `http` or `https` URLs with a non-empty host. It SHALL lowercase scheme and DNS host, preserve Unicode host form, canonicalize IP-literal hosts, strip fragments, drop an empty root slash, and enforce a normalized length of at most 2048 characters. A supplied port SHALL be decimal and between 1 and 65535.

#### Scenario: Canonical URL
- **WHEN** a URL with a default port, fragment, and empty root slash is normalized
- **THEN** the default port and fragment are removed from the normalized URL, the root slash is dropped, the lowercase host is retained, and the explicit default port is returned separately

#### Scenario: Non-default port retained
- **WHEN** a URL uses a non-default explicit port
- **THEN** the port is retained in the value and stored separately

#### Scenario: Path and query preserved
- **WHEN** a URL has a path and query string
- **THEN** their case, order, delimiters, and percent-encoded octets are preserved without decoding or re-encoding

#### Scenario: URL too long
- **WHEN** a normalized URL exceeds 2048 characters
- **THEN** normalization fails with a descriptive error

#### Scenario: Invalid URL structure rejected
- **WHEN** a URL has an unsupported scheme, missing host, malformed IP literal, invalid brackets, or invalid port
- **THEN** normalization fails with a stable error code and descriptive message

### Requirement: Normalization result shape
A successful normalization SHALL return the exact `IocType`, canonical `iocValue`, nullable explicit `port`, nullable lowercase full `urlHost`, and nullable `parentDomain`. IP results SHALL omit relation fields. URL results SHALL include `urlHost` and its resolved parent domain. Domain results SHALL leave `urlHost` NULL and include the resolved parent domain.

#### Scenario: Domain result fields
- **WHEN** `Api.Example.CO.UK.` is normalized
- **THEN** the result is type Domain, value `api.example.co.uk`, NULL port, NULL urlHost, and parentDomain `example.co.uk`

#### Scenario: IPv4 result fields
- **WHEN** `1.2.3.4:443` is normalized
- **THEN** the result is type Ipv4, value `1.2.3.4`, port `443`, and has no relation fields

#### Scenario: URL result fields
- **WHEN** `HTTPS://Api.Example.CO.UK:443/a` is normalized
- **THEN** the result is type Url, value `https://api.example.co.uk/a`, port `443`, urlHost `api.example.co.uk`, and parentDomain `example.co.uk`

### Requirement: Query type auto-classification
The system SHALL trim the query and auto-classify it as Ipv4, Ipv6, Url, or Domain using strict IP → URL-marker → domain precedence. It SHALL return a normalized result or a typed `InvalidInput` failure. The classifier SHALL have no dependency on HTTP; E09 maps its failures to HTTP 400 ProblemDetails.

#### Scenario: IP detected
- **WHEN** a bare or port-bearing IPv4/IPv6 is provided
- **THEN** it is classified as the exact Ipv4 or Ipv6 type and normalized

#### Scenario: URL detected
- **WHEN** a value starts with `http(s)://` or contains `://`
- **THEN** it is handled as a URL attempt and either returns a normalized Url result or a URL-specific validation failure

#### Scenario: Domain detected
- **WHEN** a value is a valid domain
- **THEN** it is classified as a domain

#### Scenario: Invalid input
- **WHEN** a value matches no supported type
- **THEN** classification returns a stable `InvalidInput` code and safe descriptive message

#### Scenario: Malformed URL does not fall through
- **WHEN** a value contains `://` but is not a valid HTTP or HTTPS URL
- **THEN** classification returns a URL-specific failure and does not try to classify it as a domain
