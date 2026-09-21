## Purpose

Provide a public, interoperable MCP interface to the platform's existing IOC data so website and external clients can investigate indicators through bounded, read-only tools.

## ADDED Requirements

### Requirement: Anonymous remote MCP access

The system SHALL expose a documented Streamable HTTP MCP endpoint at `/mcp` that standards-compliant remote clients can initialize, discover, and call without credentials. The endpoint SHALL expose only read-only IOC tools and SHALL not require a website session. Tool responses SHALL use stable structured fields and describe whether a result is complete or truncated.

#### Scenario: External client discovers tools
- **WHEN** a client without a token connects to `/mcp` and lists tools
- **THEN** it can discover the public IOC tools and their input and output schemas

#### Scenario: Client tries a non-public operation
- **WHEN** a client requests a tool outside the public IOC allowlist
- **THEN** no Admin action, data mutation, AI report generation, source-code access, or external enrichment is performed

### Requirement: Public IOC query tools

The MCP server SHALL provide tools to list provider names, search an exact IOC value, get an IOC group by id, browse IOC groups, and page through relations for a selected group. Search normalization, supported filters, sorting, pagination, unknown-id behavior, and source attribution SHALL match the corresponding public IOC query contracts. Search SHALL NOT add fuzzy matching or a natural-language search index. MCP input schemas SHALL reject unsupported parameters, and results SHALL identify IOC ids and provider records so clients can attribute findings.

#### Scenario: Exact value lookup
- **WHEN** a client searches for a valid IOC value present in the public data
- **THEN** the tool returns the matching IOC groups using the public exact-search semantics and their source attribution

#### Scenario: Compare related indicators
- **WHEN** a client requests a relation page for an IOC group
- **THEN** it receives the same relation family and bounded page represented by the public relation query

#### Scenario: Unsupported or missing input
- **WHEN** a client supplies an unsupported filter or an unknown IOC id
- **THEN** the tool returns a safe validation or not-found result without revealing internal data

### Requirement: Public data and resource boundary

Each tool SHALL return only data already available from anonymous IOC query endpoints, with finite page sizes, response-size limits, cancellation, and query timeouts. If a result must be shortened, the response SHALL disclose what was omitted and SHALL NOT present a partial result as complete. MCP calls SHALL be subject to anonymous request and concurrency limits; excess calls SHALL receive a retryable limit response. Indicator URLs SHALL be treated as data and SHALL NOT be fetched by the server.

#### Scenario: Large IOC evidence result
- **WHEN** public provider evidence exceeds the tool response budget
- **THEN** the result remains within the budget and explicitly reports truncation or omitted records

#### Scenario: Anonymous client exceeds the MCP budget
- **WHEN** a client exceeds an enforced MCP request or concurrency limit
- **THEN** the server rejects the call with a rate-limit response and a retry indication

