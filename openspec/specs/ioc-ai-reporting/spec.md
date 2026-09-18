# ioc-ai-reporting Specification

## Purpose

Bir IOC grubunun mevcut, kaynaklara atfedilmiş kanıtından kullanıcı isteğiyle AI raporu üretir ve raporun dayandığı veri ile kuralları denetlenebilir biçimde gösterir.

## Requirements

### Requirement: Public on-demand generation

`POST /api/iocs/{id}/ai/reports` SHALL allow anonymous and authenticated callers and SHALL resolve the same complete IOC group represented by `GET /api/iocs/{id}`. The request SHALL contain no body: the URL IOC id is the only client input; evidence, report instructions, and prompt text SHALL be server-owned. Unknown or malformed ids SHALL use the existing 404 or 400 error semantics. Generation SHALL be initiated only by an explicit user action; anonymous IOC detail browsing SHALL remain available.

#### Scenario: Signed-in analyst generates a report
- **WHEN** a signed-in user clicks the report action for an existing IOC while AI is configured
- **THEN** the server builds the report from that IOC group's source-attributed, request-scoped evidence projection and streams the response

#### Scenario: Anonymous visitor views IOC detail
- **WHEN** an anonymous visitor opens an IOC detail page
- **THEN** the IOC data remains visible and the report action can start an anonymous model call

#### Scenario: Non-Admin user generates a report
- **WHEN** a signed-in User (not an Admin) clicks the report action for an existing IOC
- **THEN** the same authenticated report endpoint accepts the request and the server builds the report from its own IOC snapshot

### Requirement: Evidence-bounded report

The model SHALL receive only a size-bounded, deterministic projection of the selected IOC group, with stable source labels and an explicit omitted-source count if the group exceeds the limit. The report SHALL use Turkish CTI terminology, separate observed facts from analyst inferences, identify missing evidence, include the published policy version and generation time, and cite supplied source labels for factual claims. It SHALL not present unobserved malware attribution, confidence, attack history, or mitigation efficacy as fact. Generated SIEM, Sigma, or YARA ideas SHALL be clearly marked as unverified suggestions and SHALL never be executed automatically.

#### Scenario: Evidence is insufficient
- **WHEN** the IOC has sparse or conflicting provider records
- **THEN** the report states uncertainty and shows the relevant source labels instead of inventing a definitive conclusion

#### Scenario: Input exceeds the model budget
- **WHEN** the IOC has more source data than the configured input budget
- **THEN** the server applies a deterministic limit and the report identifies how many sources were omitted

### Requirement: Streaming lifecycle and local download

The generation response SHALL use a documented same-origin event stream with `meta`, `delta`, `complete`, and `error` events. A pre-stream validation failure SHALL use the shared ProblemDetails format; an error after streaming begins SHALL emit a safe `error` event and SHALL not emit `complete`. The client SHALL treat text as provisional until `complete`; after final validation, it SHALL display the report and offer a local download. The server SHALL not persist the report text, evidence snapshot, or report history.

#### Scenario: Upstream fails mid-stream
- **WHEN** the upstream model fails after the first delta
- **THEN** the client receives an error event, displays the run as failed, and does not offer a download

#### Scenario: Report completes
- **WHEN** the model finishes and required sections and source references validate
- **THEN** the server emits `complete` and the client displays the report and enables download

### Requirement: Ephemeral report and resource bounds

The validated report SHALL remain available only in the current browser page session, with instruction version and generation time shown. The model identifier SHALL remain server-owned and SHALL not be displayed in the IOC-page AI component. Reloading or leaving the page SHALL discard the report. Generation SHALL enforce per-user request rate, concurrent-run, input-size, output-size, and timeout limits and SHALL return a stable 429 or safe failure when exceeded. Output SHALL render without raw HTML or executable links. AI input, output, and API keys SHALL be absent from routine request logs.

#### Scenario: User returns to the detail page
- **WHEN** a user reloads or leaves an IOC detail page after a completed report
- **THEN** no prior report is retrieved and the user can deliberately generate a new one
