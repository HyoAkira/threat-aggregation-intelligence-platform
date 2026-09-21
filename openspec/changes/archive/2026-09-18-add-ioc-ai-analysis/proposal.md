## Why

The IOC detail page presents source-attributed evidence, but it offers no workflow for turning that evidence into a consistent analyst assessment. The platform needs an on-demand Turkish report using an OpenAI-compatible model configured by an administrator.

## What Changes

- Let administrators configure the AI server Base URL and model ID in a separate settings area, while storing the API key through the existing encrypted, write-only credential endpoint.
- Ship default, versioned report instructions in a `system.md`-style file. Let administrators review, edit, and publish new versions from the Admin panel, while keeping non-editable security and data-access boundaries separate.
- Add an on-demand report action for everyone on the IOC detail page. The backend sends a bounded, source-attributed snapshot of the IOC group and the published report instructions to the model, streams the response, and lets the user download the completed report.
- Separate observations, analyst inferences, uncertainty, and recommended actions in each report. Cite source identifiers for supported claims and disclose missing evidence. Do not automatically fetch IOC URLs, perform external enrichment, block indicators, or execute detection rules.
- Protect AI generation endpoints with server-side rate and concurrency limits, secret-safe logging, and a defined streaming error contract; authentication is optional for the public report action.

## Capabilities

### New Capabilities

- `ai-provider-configuration`: Admin-only OpenAI-compatible server settings and credential status.
- `ai-report-policy-management`: Administrator-edited, versioned report instructions kept separate from immutable application security controls.
- `ioc-ai-reporting`: On-demand, source-cited Turkish reports generated from IOC evidence, streamed, displayed, and downloadable without server-side report history.

### Modified Capabilities

- `admin-frontend-runtime-settings`: Add a separate AI configuration area to `/admin/settings` without adding the AI key or configuration to the existing provider/log settings PUT contract.

## Impact

The .NET API, Application, and Infrastructure layers gain AI settings, report-instruction, reporting, and OpenAI-compatible client contracts. The change adds only the database storage needed for non-secret AI settings and report-policy versions, `/api/admin/ai/*` and `/api/iocs/{id}/ai/*` endpoints, and React updates to the IOC detail and Admin settings pages. It reuses the existing `OpenAICompatible.ServerApiKey` secret identity, `ISecretStore`, and JWT authentication. The in-progress `migrate-provider-api-keys-to-encrypted-storage` change is a dependency; this change does not redefine its encryption implementation.
