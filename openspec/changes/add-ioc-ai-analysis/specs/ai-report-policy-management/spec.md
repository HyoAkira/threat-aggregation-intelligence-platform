## Purpose

Raporun analist tarafından istenen dil, bölüm ve yorumlama kurallarını Admin panelinden yönetirken her çıktının hangi yayımlanmış yönerge sürümüyle üretildiğini izlenebilir kılar.

## ADDED Requirements

### Requirement: Versioned editable report instructions

The system SHALL initialize a default Turkish report instruction document and SHALL let an Admin read the current text, edit it, and publish a new immutable version. Publication SHALL validate non-empty text, a documented maximum length, and an optimistic concurrency token; invalid or conflicting updates SHALL retain the previously active version. A report SHALL pin the active version at generation start, so later edits do not change its provenance.

#### Scenario: Admin publishes revised report rules
- **WHEN** an Admin edits and publishes a valid instruction document
- **THEN** the next generation uses a new version and earlier reports retain their original version reference

#### Scenario: Concurrent edit is rejected
- **WHEN** an Admin publishes against a stale version token
- **THEN** the API reports a conflict and preserves the active document

### Requirement: Administrative visibility and protected boundary

Only Admins SHALL read or publish the editable instructions. The editable text SHALL control report style, sections, language, and analytical rules, but SHALL not grant model tools, widen the IOC data scope, expose secrets, change endpoint authorization, or disable server-side output checks. The system SHALL keep the non-editable safety boundary separate from this text.

#### Scenario: Instructions request wider access
- **WHEN** an Admin-authored instruction asks the model to fetch an IOC URL or search other records
- **THEN** the generation operation still supplies only the selected IOC snapshot and no network or database tool

### Requirement: Recover previous instruction content

An Admin SHALL be able to inspect previous published versions and restore prior content by publishing it as a new version; historical versions SHALL remain immutable.

#### Scenario: Admin restores an earlier policy
- **WHEN** an Admin republishes the content of a previous version
- **THEN** a new active version is created and historical report version references remain unchanged

