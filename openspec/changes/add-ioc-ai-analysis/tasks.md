## 1. Dependency and persistence

- [ ] 1.1 Confirm `migrate-provider-api-keys-to-encrypted-storage` has completed with `OpenAICompatible.ServerApiKey`, `ISecretStore`, and a configured root key; verify its credential integration tests pass before wiring AI inference.
- [ ] 1.2 Add additive EF Core schema for AI connection settings, immutable report policy versions and active pointer; verify migration applies to an existing database without altering IOC/provider rows.
- [ ] 1.3 Add `AiReport` and `AiChatTurn` persistence with owner `User.Id`, IOC identity, snapshot, model/policy provenance, and indexes; verify migration and repository tests enforce owner-scoped reads.

## 2. Admin connection and report rules

- [ ] 2.1 Implement Admin-only `GET/PUT /api/admin/ai/settings` with atomic validation and credential-status metadata; verify valid save, invalid save rollback, and 401/403 API tests.
- [ ] 2.2 Implement outbound target policy for public HTTPS and deployment-allowlisted local/private/HTTP origins, including resolved-address check and disabled redirects; verify SSRF and DNS/redirect cases with controlled HTTP tests.
- [ ] 2.3 Add the default Turkish report instruction resource and idempotent version-1 seed; verify repeated startup preserves a previously published Admin version.
- [ ] 2.4 Implement Admin policy read/history/publish endpoints with length validation and expected-version conflict behavior; verify authorization, immutable history, 409 conflict, and restore-as-new-version tests.
- [ ] 2.5 Add an AI section to `/admin/settings` for connection values, key set/replace/clear, instruction editing/history, and accessible status; verify frontend tests keep plaintext keys out of query caches, URLs, and provider/log PUT requests.

## 3. Model adapter and bounded evidence

- [ ] 3.1 Implement a typed OpenAI-compatible chat-completions adapter with model ID, optional operation-scoped key resolution, streaming parser, cancellation, size/time limits, and safe error mapping; verify keyless, keyed, malformed upstream, timeout, and cancellation fixtures.
- [ ] 3.2 Project `IocGroupResult` into deterministic `S1...Sn` evidence with explicit truncation, string/byte budgets, and excluded raw provider JSON/links; verify multi-provider, port-variant, false-positive, and oversized-group tests.
- [ ] 3.3 Build version-pinned report messages from fixed safety instructions, Admin report policy, and frozen evidence; validate required sections and source labels after completion; verify sparse/conflicting evidence and invented-source fixtures fail safely.

## 4. Report API and streaming

- [ ] 4.1 Add a separate authenticated IOC AI controller for generation, list, and detail; verify anonymous POST receives 401 while existing anonymous IOC GETs still succeed, and foreign report ids cannot be read.
- [ ] 4.2 Implement backend-owned `meta/delta/complete/error` SSE frames with pre-stream ProblemDetails, cancellation propagation, and commit-before-complete; verify success, mid-stream failure, and abort integration tests leave no completed partial report.
- [ ] 4.3 Compare the stored canonical snapshot digest with current IOC evidence on report reads and expose freshness/provenance; verify a provider-source update marks an old report stale without changing its stored content.
- [ ] 4.4 Add per-user request and concurrency limits plus a host-wide cap and safe `429`/`Retry-After`; verify throttled and concurrent calls make no extra upstream request.

## 5. IOC report frontend

- [ ] 5.1 Add a same-origin POST streaming client that sends Bearer auth, parses split SSE frames, handles pre-stream ProblemDetails/401, and supports `AbortController`; verify parser and session-loss tests.
- [ ] 5.2 Add the report action to `/iocs/$id` with sign-in prompt, provisional progress, cancel/retry, completed report list, source citations, freshness/model/policy metadata, and accessible error states; verify user interaction tests for anonymous, success, and failed stream paths.
- [ ] 5.3 Render completed Markdown without raw HTML or executable links and keep provisional output as plain text; verify a malicious Markdown fixture cannot create an executable link or HTML node.

## 6. IOC-scoped chat

- [ ] 6.1 Add owner-bound report chat and history endpoints that load only the report's frozen IOC snapshot and last eight canonical messages, with no tools or arbitrary role history; verify foreign report, mismatched IOC, oversized message, and context-window tests.
- [ ] 6.2 Stream chat answers with the report event contract and commit only complete turns; verify cancellation and upstream failure do not persist partial assistant answers.
- [ ] 6.3 Add a report-bound chat panel with scoped refusal, loading, cancel, retry, and history states; verify a user cannot submit chat before report completion and the panel handles failed streams accessibly.

## 7. Operational verification

- [ ] 7.1 Add safe structured telemetry and redact prompt, evidence, output, Authorization, credential, and upstream error body values; verify log-capture tests contain only identifiers/status/timing.
- [ ] 7.2 Document configuration, local-model container networking, supported chat-completions subset, data sent upstream, prompt-version workflow, limits, and rollback; verify docs match the exposed API contract and example deployment settings.
- [ ] 7.3 Run backend unit/integration suites, frontend tests/build, a controlled local-model or stub end-to-end generation/chat run, nginx SSE flush/timeout check, and `openspec validate add-ioc-ai-analysis --strict`; record outcomes before enabling production AI.
