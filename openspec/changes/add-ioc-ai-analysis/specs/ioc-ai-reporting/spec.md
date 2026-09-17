## Purpose

Bir IOC grubunun mevcut, kaynaklara atfedilmiş kanıtından kullanıcı isteğiyle Türkçe AI raporu üretir ve raporun dayandığı veri ile kuralları denetlenebilir biçimde gösterir.

## ADDED Requirements

### Requirement: Authenticated on-demand generation

`POST /api/iocs/{id}/ai/reports` SHALL require an authenticated user and SHALL resolve the same complete IOC group represented by `GET /api/iocs/{id}`. The request SHALL accept the IOC id only, not client-supplied evidence or prompt text. Unknown or malformed ids SHALL use the existing 404 or 400 error semantics. Generation SHALL be initiated only by an explicit user action; anonymous IOC detail browsing SHALL remain available.

#### Scenario: Signed-in analyst generates a report
- **WHEN** a signed-in user clicks the report action for an existing IOC while AI is configured
- **THEN** the server builds the report from that IOC group's source-attributed snapshot and streams the response

#### Scenario: Anonymous visitor views IOC detail
- **WHEN** an anonymous visitor opens an IOC detail page
- **THEN** the IOC data remains visible and report generation is offered through sign-in, without an anonymous model call

### Requirement: Evidence-bounded report

The model SHALL receive only a size-bounded, deterministic projection of the selected IOC group, with stable source labels and an explicit omitted-source count if the group exceeds the limit. The report SHALL use Turkish CTI terminology, separate observed facts from analyst inferences, identify missing evidence, include the published policy version and generation time, and cite supplied source labels for factual claims. It SHALL not present unobserved malware attribution, confidence, attack history, or mitigation efficacy as fact. Generated SIEM, Sigma, or YARA ideas SHALL be clearly marked as unverified suggestions and SHALL never be executed automatically.

#### Scenario: Evidence is insufficient
- **WHEN** the IOC has sparse or conflicting provider records
- **THEN** the report states uncertainty and shows the relevant source labels instead of inventing a definitive conclusion

#### Scenario: Input exceeds the model budget
- **WHEN** the IOC has more source data than the configured input budget
- **THEN** the server applies a deterministic limit and the report identifies how many sources were omitted

### Requirement: Streaming lifecycle and completed report record

The generation response SHALL use a documented same-origin event stream with `meta`, `delta`, `complete`, and `error` events. A pre-stream validation failure SHALL use the shared ProblemDetails format; an error after streaming begins SHALL emit a safe `error` event and SHALL not emit `complete`. Only a fully received and validated report SHALL be stored with its owner, IOC identity, frozen evidence snapshot, model ID, instruction version, and timestamp. A cancelled or failed stream SHALL not create a completed report. The client SHALL treat text as provisional until `complete`.

#### Scenario: Upstream fails mid-stream
- **WHEN** the upstream model fails after the first delta
- **THEN** the client receives an error event, displays the run as failed, and cannot open it as a completed report

#### Scenario: Report completes
- **WHEN** the model finishes and required sections and source references validate
- **THEN** the server stores the report and emits `complete` with its report id

### Requirement: Report access, freshness, and resource bounds

Reading a stored report SHALL require authentication and ownership. The UI SHALL display the report's model, instruction version, generation time, and whether the current IOC evidence differs from its frozen snapshot. Generation SHALL enforce per-user request rate, concurrent-run, input-size, output-size, and timeout limits and SHALL return a stable 429 or safe failure when exceeded. Output SHALL render without raw HTML or executable links. AI input, output, and API keys SHALL be absent from routine request logs.

#### Scenario: Another user requests a report
- **WHEN** a different signed-in user requests a report id they do not own
- **THEN** the API denies access without returning report content

#### Scenario: IOC evidence changes after report generation
- **WHEN** a provider record changes after a report was completed
- **THEN** the existing report remains linked to its original snapshot and the UI indicates that a new report is needed for current evidence

