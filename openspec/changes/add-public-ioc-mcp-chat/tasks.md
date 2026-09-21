## 1. Public IOC MCP surface

- [ ] 1.1 Add the C# MCP server package, stateless `/mcp` registration, anonymous route policy, and accepted-host/origin handling; verify a remote MCP test client can initialize and list tools without a token while Admin endpoints still reject anonymous requests.
- [ ] 1.2 Implement the five allowlisted read-only IOC tools over the existing public query handlers and public-only output projection; verify fixture comparisons with REST for providers, exact search, detail, browse, and relations, including filters and page metadata.
- [ ] 1.3 Add tool argument validation, finite page/payload/time limits, disclosed truncation, cancellation, and safe invalid/not-found errors; verify oversized and malformed tool-call tests and confirm no mutation, report, external-fetch, file, or Admin tool is discoverable.
- [ ] 1.4 Add separate external MCP request and concurrency limits with trusted client-address handling and a host cap; verify 429/retry behavior and forged-forwarded-header tests.

## 2. Dedicated chat configuration

- [ ] 2.1 Add chat settings persistence and migration with `enabled=false` by default; verify the migration starts on an existing database and leaves current AI report settings unchanged.
- [ ] 2.2 Register the separate chat secret identity and Admin-only chat settings API using the existing write-only credential contract; verify role checks, input validation, key-status-only reads, and independence from the report key.
- [x] 2.3 Add an OpenAI-compatible chat adapter that sends tool definitions, parses tool calls and final text, uses only the chat secret, and detects unsupported tool behavior; verify mocked upstream cases for valid calls, malformed calls, timeouts, and models without tool support.

## 3. Bounded chat orchestration and API

- [ ] 3.1 Add the in-process MCP client path and `POST /api/iocs/{id}/chat`, with selected-IOC resolution and bounded, untrusted client history; verify anonymous access, unknown-id handling, and that evidence retrieval uses MCP tools.
- [ ] 3.2 Implement the allowlisted tool loop with validated arguments and finite round, tool, page, evidence, output, and time budgets; verify attempts to call an unknown tool, issue writes, or exceed each budget fail safely.
- [ ] 3.3 Add server-owned IOC-only instructions and final-answer checks for cited IOC/provider identifiers, fact-versus-assessment labels, uncertainty, refusal, and safe rendering data; verify cross-IOC comparisons, missing evidence, unrelated requests, source-code/secret requests, and prompt injection in provider fields.
- [ ] 3.4 Add separate chat rate/concurrency limits, stable ProblemDetails failures, and content-free telemetry; verify limit, disabled, credential-unavailable, upstream-failure, and cancellation paths without a model call when preconditions fail.

## 4. Frontend experience

- [x] 4.1 Add typed chat API handling and an IOC-page conversation state kept only in React memory; verify a refresh starts with no transcript and no browser or server persistence path is used.
- [x] 4.2 Add the chat panel to the IOC detail page without triggering the existing AI report action, showing current/other IOC references, labeled assessments, uncertainty, model-data disclosure, and safe error states; verify anonymous compare and disabled/failure UI behavior.
- [x] 4.3 Add a separate Admin chat settings section, schemas, query keys, and credential controls; verify independent saves, accessible validation, draft preservation after failure, and clearing of the plaintext key input after success.

## 5. Integration and release readiness

- [ ] 5.1 Run end-to-end contract tests with a remote MCP client and a fake tool-capable model for anonymous discovery, exact IOC lookup, cross-IOC chat, rate limits, and protected-route isolation; verify existing AI report tests remain green.
- [ ] 5.2 Document the public MCP connection, tool schemas, deployment quotas and proxy handling, chat model tool-call requirement, privacy disclosure, and enable/rollback steps; verify the documented flow against the deployed test configuration.
