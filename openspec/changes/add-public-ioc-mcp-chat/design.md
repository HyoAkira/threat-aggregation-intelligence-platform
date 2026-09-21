## Context

See [proposal.md](proposal.md) for motivation and the five delta specs for observable behavior. The API is ASP.NET Core on .NET 10, with a fallback authenticated policy and explicit anonymous IOC controllers. `IocsController` already implements provider listing, exact search, detail, browse, and paged relations through application queries and `IIocQueryReader`. Its public result includes source-attributed provider records. The existing AI report has an Admin-managed OpenAI-compatible connection, a write-only secret, a text-only streaming client, and a report-specific in-process limiter. The React IOC route already contains a separate AI report action, while `/admin/settings` contains report model controls. The current report client does not parse model tool calls and must not be reused unchanged for chat.

The [MCP specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools) defines tool schemas and structured results. The [C# SDK transport guide](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html) supports stateless Streamable HTTP for remote clients and in-memory transports for an embedded client/server pair.

## Goals / Non-Goals

**Goals:**

- Keep one public IOC query contract behind the REST controllers and MCP tools, with bounded structured results and stable source identifiers.
- Make the chatbot a client of the same MCP tool contract used by external clients, while enforcing its own smaller per-turn budget.
- Keep the chat model, credential, limits, and failures independent from AI report generation.

**Non-Goals:**

- Adding a generic web browser, external threat enrichment, a fuzzy search index, or natural-language search to the MCP server.
- Persisting chat transcripts, model results, or server-side MCP sessions.
- Exposing report generation, Admin operations, database queries, arbitrary HTTP requests, files, or code as MCP tools.

## Decisions

### 1. Host a stateless public MCP endpoint in the existing API

Add the maintained `ModelContextProtocol.AspNetCore` package and map `/mcp` as stateless Streamable HTTP with anonymous authorization. Register a fixed tool collection: `ioc_list_providers`, `ioc_search_exact`, `ioc_get_detail`, `ioc_browse`, and `ioc_get_relations`. Each tool calls the same application query/service path as its REST counterpart rather than constructing independent SQL. Define explicit input/output schemas, descriptive tool metadata, bounded pages, typed errors, and a public-only projection. Keep IOC ids, provider record identifiers, page totals, and omission metadata in results. No tool issues writes or fetches a supplied URL.

Stateless HTTP fits unrelated remote MCP clients and avoids session affinity. Stdio would tie usage to local processes; a separate service would duplicate deployment and query wiring. The website calls its chat API rather than connecting directly to `/mcp`; external clients use the documented MCP URL. Validate accepted hosts and request `Origin` on the MCP route, and leave cross-origin browser access closed unless an explicit origin allowlist is later configured. CORS is not an authentication mechanism.

### 2. Make the chatbot an MCP client with a bounded tool loop

Add a server-side chat orchestrator that invokes the registered MCP tools through an in-process MCP client transport. This keeps the same tool names, schemas, and outputs as remote clients without self-HTTP routing or consuming a shared external-client IP quota. The orchestrator receives the selected IOC id and a size-limited, client-supplied transcript; it treats the transcript as untrusted conversational context. It sends server-owned rules and only the five IOC tool definitions to a dedicated tool-call-capable OpenAI-compatible client. Model-proposed tool names and arguments are validated against the allowlist and schemas, then executed through MCP. A finite call/round budget, page budget, total evidence budget, timeout, and cancellation token bound the loop. An answer about an IOC must follow at least one successful MCP call in the current turn; the selected IOC is resolved by the server, not accepted as client-supplied evidence.

The current `OpenAiCompatibleChatClient` handles text deltas and the report secret only, so chat gets a separate tool-call adapter and configuration reader. It can reuse URL validation and safe upstream error conventions. The adapter must detect unsupported `tools`/`tool_calls` behavior and fail closed rather than accept an ungrounded completion. A direct call to the IOC query reader from chat was considered, but it would bypass the MCP tool contract that the user wants to reuse.

### 3. Return only completed, checked answers

Use `POST /api/iocs/{id}/chat` with a complete JSON response. The server buffers the final model output, checks length and format, and verifies that cited IOC/provider identifiers occur in MCP results from that turn before returning it. The instructions require facts and labeled assessments to be distinguished, conflicting or missing evidence to be acknowledged, and unrelated or disclosure-seeking requests to be refused. Tool results and client messages are lower-trust data; neither can change the system rules. The output check rejects unexpected tool names, ungrounded substantive answers, and obvious secret/code disclosure patterns. It is a guardrail, not a semantic proof of every claim; prompt-injection and citation evaluations remain required.

A complete response avoids displaying provisional claims before citation checks. Streaming final text was considered but adds a partial-answer lifecycle and cannot guarantee validation before display. Use existing ProblemDetails patterns for pre-response validation, disabled, limit, and upstream errors. Render the answer as safe text or the existing constrained Markdown component, with local IOC links generated from validated ids rather than model-supplied HTML.

### 4. Separate Admin chat configuration and secret identity

Persist chat `baseUrl`, `modelId`, and `enabled` in a new settings record, defaulting to disabled. Add Admin-only `GET/PUT /api/admin/ai/chat/settings`; register a distinct managed secret identity (for example `OpenAICompatible.ChatApiKey`) in the existing write-only credential store and reuse `/api/admin/credentials/{identity}` for set/replace/clear/status. Never return the key. The Admin UI adds a chat section with independent save state and a tool-call support notice. A model choice remains fully Admin-controlled; no model vendor or price is hardcoded. The AI report configuration and its key are read and written separately.

An Admin-configured destination may be remote or local, consistent with the report connection. User input cannot select the destination. Chat checks settings and credential availability before calling the model. An incompatible model produces a stable safe error; browsing and reports continue.

### 5. Bound public cost at both ingress points

Use separate finite request and concurrency budgets for external MCP calls and chat runs, plus a host-wide cap. For unauthenticated requests, partition the request budget by a server-derived client address only after trusted-proxy handling; never trust a raw caller-supplied forwarded address. Also enforce an aggregate cap because shared addresses and rotating addresses weaken per-client fairness. Keep limits deployment-configurable with documented finite defaults and expose retry information on 429 responses. The in-process chat MCP client does not consume the external MCP quota, but every chat turn has its own tool-count, evidence-size, and query-time limits. Avoid transcript, key, prompt, and tool-evidence bodies in routine logs; log outcome, elapsed time, count, and correlation id instead.

The existing `AiReportThrottle` is report-specific and groups all anonymous visitors under one key; sharing it would couple both features and cause avoidable contention. Chat and MCP limiters must also account for multiple API replicas at the gateway or a shared limiter if the deployment scales beyond one instance.

### 6. Keep browser state in memory

Implement chat state in React memory for the current page session. Do not use local storage, session storage, URL state, React Query persistence, or a server conversation table. On refresh, the component starts empty; each submitted turn sends only the bounded context needed for follow-ups. The selected IOC remains the entry point, while MCP tools can resolve other indicators without page navigation. The UI displays sources, labeled assessments, loading, disabled, retry, and failure states separately from the existing AI report component. Inform users that their messages and selected IOC evidence are sent to the Admin-configured model endpoint.

## Risks / Trade-offs

- **Anonymous abuse or model cost** → Apply separate per-client and global limits, strict model/tool budgets, cancellation, and an Admin enable switch; monitor aggregate usage without logging content.
- **NATs and multiple replicas make IP quotas imperfect** → Keep a host-wide cap and enforce an aggregate budget at the gateway or shared store when scaling; document the deployment requirement.
- **Malicious provider fields or client transcripts can prompt-inject the model** → Treat them as data, restrict executable tools in code, validate outputs and citations, and exercise adversarial tests.
- **A valid citation does not prove every sentence** → Require explicit fact/inference labels, prefer refusal on weak evidence, and evaluate representative IOC comparisons and unsupported questions before enabling chat.
- **Large public provider payloads may exceed model or MCP budgets** → Use deterministic size limits and disclose omissions; never silently claim complete evidence.
- **Some inexpensive OpenAI-compatible models do not implement tool calls consistently** → Fail closed on unsupported responses and make tool-call support clear in Admin settings.
- **A buffered answer may feel slower than a streamed one** → Show a progress state and keep an end-to-end timeout; buffering allows validation before display.

## Migration Plan

1. Add the new chat settings migration and managed secret identity with chat disabled by default; existing report settings and keys remain intact.
2. Deploy the read-only MCP tools and ingress limits, then verify anonymous discovery/calls, REST parity, and protected-route isolation with a remote MCP client.
3. Deploy the chat orchestrator and Admin/frontend controls. Configure a tool-capable model and enable chat only after contract, abuse, and adversarial checks pass.
4. Roll back chat immediately through its Admin enabled flag if the model or cost behavior is unacceptable. If MCP itself must be withdrawn, revert the API route deployment; the additional disabled chat settings record can remain without affecting existing features.
