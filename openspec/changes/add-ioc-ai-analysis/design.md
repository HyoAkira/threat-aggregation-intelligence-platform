## Context

See [proposal.md](proposal.md). The current `GET /api/iocs/{id}` resolves a row id to a complete cross-provider `IocGroupResult`; `IocSourceResult` preserves provider attribution, tags, timestamps, false-positive state, and optional provider fields. `IocsController` has class-level `[AllowAnonymous]`. React renders `/iocs/$id` as a public TanStack Router page and calls the same-origin `/api` through a JSON-only `apiFetch`. `/admin/settings` currently writes a complete provider/log snapshot. The in-progress credential migration has already introduced `OpenAICompatible.ServerApiKey`, `ISecretStore`, and non-secret `OpenAICompatible:BaseUrl` configuration; its secret-storage work must be completed before this change uses it. The API targets .NET 10, uses JWT Bearer auth with username and role claims, and has no AI rate limiter yet.

The user chose Admin-editable report rules. The attached draft also includes IOC-scoped chat, so this design includes it after report generation. The report is generated only from the stored IOC group; there is no IOC URL retrieval or external enrichment.

## Goals / Non-Goals

**Goals:** Preserve public IOC browsing; make costly inference an authenticated, explicit action; support OpenAI-compatible chat completions through a server-side adapter; give Admins editable, versioned report rules; keep each report reproducible against its frozen evidence and rule version; stream visible progress with clear failure semantics; limit outbound destinations and model context.

**Non-Goals:** Autonomous agents, Semantic Kernel plugins, RAG across the IOC database, IOC URL fetching, automated SIEM/YARA/Sigma deployment, universal compatibility with every vendor-specific API variant, or treating model answers as verified threat intelligence.

## Decisions

### 1. The API owns the model connection

The browser calls same-origin .NET endpoints and never receives the upstream Base URL as a request target or the API key value. `POST /api/iocs/{id}/ai/reports` and report-chat endpoints live in a **separate AI controller without `[AllowAnonymous]`**, because the existing IOC controller is anonymous at class level. Admin configuration lives under `/api/admin/ai/*`, inheriting the existing Admin convention. Ownership is resolved from the JWT username to the database `User.Id` before accessing persisted reports; the current JWT has no id claim.

The AI adapter uses a typed `HttpClient` to call the configured OpenAI-compatible `/chat/completions` endpoint. The configured Base URL is normalized once; the application appends the fixed operation path rather than accepting an arbitrary request URL. Model ID is required. API key is optional for a deliberately keyless local server; when configured, it is resolved immediately before the operation and only placed in the upstream Authorization header. No singleton kernel or plaintext-key cache is used. This avoids a new orchestration framework for a flow with no tools or function calling. Semantic Kernel has a supported custom-endpoint overload and can be adopted later if orchestration is added, but it provides no required capability here ([Microsoft connector API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.openaikernelbuilderextensions.addopenaichatcompletion?view=semantic-kernel-dotnet)).

### 2. Connection settings are separate from provider settings

`GET/PUT /api/admin/ai/settings` manage `{baseUrl, modelId, enabled}` in database-backed non-secret settings, with complete-request validation and atomic update. The response joins only `configured` status from the existing `GET /api/admin/credentials/OpenAICompatible.ServerApiKey`. Set/replace/clear reuse that credential endpoint. This leaves `PUT /api/admin/settings`' provider/log snapshot unchanged and avoids concurrent changes to its contract. A compatibility read of the existing `OpenAICompatible:BaseUrl` deployment option may populate an initial draft during migration, but the active runtime source becomes the database after the Admin saves; the UI indicates which source is active.

The target policy permits public HTTPS origins by default, while private addresses, loopback, and HTTP require an explicit deployment allowlist. Reject URI credentials, query, fragment, unexpected scheme, and redirects. Validate both the configured host and the actual resolved/connect address on each operation; cap response headers/body, timeouts, and connection count. This accommodates a local model server without turning Admin input into an unrestricted SSRF primitive. OWASP recommends explicit allowlists and redirect control for server-side URL fetching ([SSRF guidance](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html)). In a container deployment, `localhost` means the API container, so local model examples should use an explicitly allowed service DNS name, not assume the host loopback.

### 3. Editable report rules are versioned data, not an executable policy

Ship a default Turkish report document as a repository resource, for example `src/ThreatIntelligence.Infrastructure/AI/Prompts/default-report.md`; seed version 1 only when no policy exists. Store immutable versions (`version`, `text`, author user id, UTC publish time) and one active-version pointer. `GET /api/admin/ai/report-policy`, version history reads, and `PUT /api/admin/ai/report-policy` with `expectedVersion` support editing, conflict detection, and restoration by republishing prior text. Check size and non-empty text before publication. Pin the version at report start.

The application keeps data access, no-tool behavior, source-label validation, output limits, and secret exclusion in code. The editable text controls language, sections, and analytical preferences; it cannot change those enforced boundaries. Build structured model messages with fixed safety instructions, the active Admin document, and the frozen IOC data as separate conceptual inputs. An OpenAI-compatible implementation may handle roles differently, so no safety property relies only on role precedence or delimiters. OWASP treats prompt filters and delimiters as layers, not complete protection ([LLM prompt-injection guidance](https://cheatsheetseries.owasp.org/cheatsheets/LLM_Prompt_Injection_Prevention_Cheat_Sheet.html)).

### 4. Generate from a bounded, source-labeled IOC snapshot

Use `IIocQueryReader.GetDetailGroupAsync(id)`; serialize an allowlisted projection rather than the entire DTO. Include normalized value/type, aggregate times, and deterministic source entries (`S1`, `S2`, …) with provider, provider record id, times, tags, false-positive state, and relevant typed fields. Do not send raw `PhishTankDetails`, arbitrary links, reporter fields, or unrelated IOC rows by default. Normalize/truncate individual strings, bound source count and total bytes, and record the omitted count. Sort deterministically so identical evidence yields the same snapshot hash. Store the canonical snapshot and digest with a completed report. On report read, compare the current projection digest and show a stale-evidence indicator; the original report and chat keep using the frozen snapshot.

The report rule asks for: summary, cited observations, analysis and uncertainty, risk scenarios, and recommended investigative/mitigation actions. It must distinguish observed data from inference, including false-positive disagreements. If the model omits required sections or invents source labels, the server marks generation failed and does not persist a completed report. Treat streaming deltas as a provisional preview; the UI renders them as plain text until validation succeeds, then renders sanitized Markdown without raw HTML or executable links. Generated detection rules are labeled unverified examples and not applied.

### 5. Stream through a backend-owned event contract

The browser uses `fetch` with `POST`, Bearer header, request body, `ReadableStream`, and `AbortController`; the current JSON-only `apiFetch` gets a sibling streaming transport that preserves its 401/session-loss and ProblemDetails behavior. Native `EventSource` is not a good fit for this existing Bearer/POST flow; `fetch` exposes response streaming and cancellation ([MDN streams](https://developer.mozilla.org/en-US/docs/Web/API/Streams_API/Using_readable_streams)). The upstream chat-completions `stream` option is documented as SSE ([OpenAI Chat Completions](https://platform.openai.com/docs/api-reference/chat/create)); the backend parses upstream chunks and emits its **own** `meta`, `delta`, `complete`, or `error` events. Browser code never parses vendor-specific chunks.

Perform authentication, configuration, IOC lookup, quota, and policy checks before sending SSE headers so these failures remain normal ProblemDetails responses. Once headers are sent, a safe `error` event terminates a failed run; `complete` is emitted only after output validation and transaction commit. Propagate request cancellation to the upstream `HttpClient`; no automatic retry of a partially streamed operation. Set `text/event-stream`, no-store, and proxy buffering controls; verify nginx timeout and flush behavior in the E14 deployment. .NET 10 supports typed SSE results, but a controller may also write and flush frames explicitly ([ASP.NET Core SSE](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)).

### 6. Persist completed reports and canonical chat turns

Add `AiReport` with opaque id, IOC row id and normalized identity, owner `User.Id`, frozen snapshot JSON/hash, model, policy version, validated Markdown, and UTC completion time. Save only after successful completion. Add `AiChatTurn` with report id, monotonically increasing sequence, user question, completed answer, and UTC time. `GET /api/iocs/{id}/ai/reports` lists only the caller's reports, `GET /api/iocs/{id}/ai/reports/{reportId}` reads one with current-evidence freshness, and chat history reads are owner-scoped. Every read and write verifies both owner and IOC identity. On re-generation, create a new report; never mutate historical reports.

`POST /api/iocs/{id}/ai/reports/{reportId}/chat` accepts one user message, not client-supplied role history. The server loads the canonical report/snapshot and only the last eight completed messages. It adds no tools or wider database retrieval. Commit a turn only after the answer is complete; failed/cancelled output remains provisional in the UI. This avoids trusting a browser-supplied prior assistant message. The model is instructed to refuse unrelated questions, but this is a probabilistic behavior; enforced isolation comes from restricted context and no tools.

### 7. Apply resource limits and safe observability

Use an endpoint-specific per-user request budget plus a concurrency limiter, with a host-wide cap and `429`/`Retry-After`; key partitions by resolved user id rather than caller-supplied IP or prompt. Bound prompt/output bytes and tokens, message length, number of sources, HTTP duration, and stored report size. ASP.NET Core supports partitioned and chained rate/concurrency policies ([Microsoft rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)). For multiple API replicas, enforce an aggregate upstream budget at the gateway or shared store because local middleware limits are per process.

Log report id, IOC id, user id, model id, policy version, duration, status, and correlation id. Do not log prompt bodies, IOC snapshot JSON, report/chat text, API keys, Authorization headers, or upstream error bodies. Return stable safe error codes for disabled/unconfigured AI, invalid target, upstream timeout/failure, output invalidity, and quota exhaustion. Inform the user in the UI that selected IOC evidence is sent to the configured model server when generation is requested.

## Risks / Trade-offs

- [Model output can be wrong or ignore a prompt boundary] → Keep no tools, bound input to one frozen IOC, validate source labels/format, label inferences, and require analyst review. Regex matching `ignore previous instructions` is not used as the primary control.
- [Streaming exposes unvalidated partial text] → Display plain provisional text and discard it from the completed-report view on error; persist only validated final content.
- [Admin-configured URL can reach internal services] → Require explicit private/HTTP allowlisting, validate connect addresses, and disable redirects; also enforce deployment egress policy.
- [“OpenAI compatible” servers differ in auth, path, role, and streaming details] → Support the chat-completions subset with fixed fixtures and a documented compatibility test; return a clear unsupported-response error. Avoid claiming universal compatibility.
- [Large IOC groups and chat history drive high latency/cost] → Deterministic snapshot truncation, token budgets, per-user/global concurrency, timeout, and visible omitted-source count.
- [Persistent reports duplicate threat data] → Keep ownership checks and database backup controls; reports retain their original snapshot for audit and should follow the deployment's data-retention process.
- [Existing credential migration and E14 proxy are concurrent work] → Sequence secret-store integration after migration; verify SSE proxy behavior when E14 is applied without editing those changes' in-progress files.

## Migration Plan

1. Complete and verify the in-progress encrypted credential-store change, including the OpenAI key identity. Deploy the root key before enabling AI.
2. Apply additive database migrations for non-secret AI settings, policy versions, reports, and chat turns. Seed the default report policy only when absent. Keep AI disabled by default.
3. Deploy backend endpoints and outbound target policy; add only approved model-server origins to deployment configuration. Existing anonymous IOC GET behavior remains unchanged.
4. Deploy the Admin configuration/editor and IOC report/chat UI. Configure Base URL, model ID, optional key, and policy; run a controlled test with a stub or approved model before enabling production traffic.
5. Verify signed-in/anonymous behavior, source grounding, cancellation, mid-stream failure, 429, SSRF cases, no secret logging, and nginx SSE flush/timeout behavior.

Rollback: disable AI generation first. Revert application deployment while retaining additive tables and report data; no existing IOC, provider, or authentication data needs rollback. Remove new schema only through a separately reviewed data-retention operation.
