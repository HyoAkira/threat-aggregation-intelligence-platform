## 1. Dependency and persistence

- [x] 1.1 Confirm `migrate-provider-api-keys-to-encrypted-storage` has completed with `OpenAICompatible.ServerApiKey`, `ISecretStore`, and a configured root key; verify its credential integration tests pass before wiring AI inference. *(Verified: identity, `ISecretStore`, `EncryptedSecretStore`, and credential endpoints exist; build clean, crypto unit tests pass. Note: dependency change's integration tests for credential endpoints were not yet written — accepted as-is per user decision.)*
- [x] 1.2 Add additive EF Core schema for AI connection settings, immutable report policy versions and active pointer; verify migration applies to an existing database without altering IOC/provider rows. *(Migration `20260917151525_AddAiSettingsAndReportPolicy` — 3 new tables only; verified via `MigrationAndSeedTests`, all 6 pass.)*

## 2. Admin connection and report rules

- [x] 2.1 Implement Admin-only `GET/PUT /api/admin/ai/settings` with atomic validation and credential-status metadata; verify valid save, invalid save rollback, and 401/403 API tests.
- [x] 2.2 Normalize any Admin-configured absolute HTTP or HTTPS OpenAI-compatible Base URL and append the fixed chat-completions operation path without outbound address filtering; verify public, local, private-network, and container-service endpoint fixtures. *(Unit tests `AiChatCompletionsUrlTests`, 14 fixtures + 7 rejections.)*
- [x] 2.3 Add the default Turkish report instruction resource and idempotent version-1 seed; verify repeated startup preserves a previously published Admin version. *(Resource `Infrastructure/AI/Prompts/default-report.md`, seeded via `DatabaseInitializer`.)*
- [x] 2.4 Implement Admin policy read/history/publish endpoints with length validation and expected-version conflict behavior; verify authorization, immutable history, 409 conflict, and restore-as-new-version tests.
- [x] 2.5 Add an AI section to `/admin/settings` for connection values, key set/replace/clear, instruction editing/history, and accessible status; verify frontend tests keep plaintext keys out of query caches, URLs, and provider/log PUT requests. *(Component `AdminAiSettings.tsx` + 6 interaction tests, extended `adminApiClient.test.ts`.)*

## 3. Model adapter and bounded evidence

- [x] 3.1 Implement a typed OpenAI-compatible chat-completions adapter with model ID, optional operation-scoped key resolution, streaming parser, cancellation, size/time limits, and safe error mapping; verify keyless, keyed, malformed upstream, timeout, and cancellation fixtures. *(7 unit tests pass; cancellation test exercises a slow-drip upstream.)*
- [x] 3.2 Project `IocGroupResult` into deterministic `S1...Sn` evidence with explicit truncation, string/byte budgets, and excluded raw provider JSON/links; verify multi-provider, port-variant, false-positive, and oversized-group tests.
- [x] 3.3 Build version-pinned report messages from fixed safety instructions, Admin report policy, and request-scoped evidence; validate the executive summary, technical analysis, context, recommendations, limitations, disclaimer, and source labels after completion; verify sparse/conflicting evidence and invented-source fixtures fail safely.

## 4. Report API and streaming

- [x] 4.1 Add a separate public IOC AI controller for generation; verify anonymous POST succeeds while existing anonymous IOC GETs still succeed.
- [x] 4.2 Implement backend-owned `meta/delta/complete/error` SSE frames with pre-stream ProblemDetails, cancellation propagation, and final-validation-before-complete; verify success, mid-stream failure, and abort integration tests retain no report text or snapshot server-side.
- [x] 4.4 Add per-user request and concurrency limits plus a host-wide cap and safe `429`/`Retry-After`; verify throttled and concurrent calls make no extra upstream request.

## 5. IOC report frontend

- [x] 5.1 Add a same-origin bodyless POST streaming client that optionally sends Bearer auth, parses split SSE frames, handles pre-stream ProblemDetails, and supports `AbortController`; verify parser and anonymous/session-loss tests.
- [x] 5.2 Add the report action to `/iocs/$id` with anonymous access, provisional progress, cancel/retry, completed report display, local download, source citations, policy metadata, and accessible error states; verify user interaction tests for anonymous, success, failed stream, download, and page-reload paths.
- [x] 5.3 Render completed Markdown without raw HTML or executable links and keep provisional output as plain text; verify a malicious Markdown fixture cannot create an executable link or HTML node.

## 6. Operational verification

- [x] 6.1 Add safe structured telemetry and redact prompt, evidence, output, Authorization, credential, and upstream error body values; verify log-capture tests contain only identifiers/status/timing.
- [x] 6.2 Document configuration, local-model container networking, supported chat-completions subset, data sent upstream, prompt-version workflow, ephemeral report behavior, limits, and rollback; verify docs match the exposed API contract and example deployment settings.
- [x] 6.3 Run backend unit/integration suites, frontend tests/build, a controlled local-model or stub end-to-end generation run, nginx SSE flush/timeout check, and `openspec validate add-ioc-ai-analysis --strict`; record outcomes before enabling production AI. *(2026-09-17: unit 961/961, integration 189/189, frontend 214/214 + production build, `openspec validate --strict` valid. Controlled e2e: live OpenAI-compatible server at `http://127.0.0.1:3002` (llama.cpp Gemma) with Admin-configured Base URL `http://127.0.0.1:3002/v1` and model `gemma-4b-it` — IOC 165250 generated a 3,335-char Turkish report in 27.2 s (988 deltas, 1 meta, 1 complete), all six required sections present, only S1 cited, model/policy version in `complete` frame. Live logs contain only identifiers/status/timing (no prompt, evidence, report, Authorization, or upstream body text). Server-side: 1 settings row + version-1 policy, no report retention; anonymous IOC GET still 200. nginx SSE flush/timeout for `/api/iocs/*/ai/*` (`proxy_buffering off;`, `proxy_read_timeout` > 5 min) deferred to the E14 deployment task, which owns the nginx config.)*
