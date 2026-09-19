# Public IOC MCP and IOC Chat

## Public MCP connection

The API exposes a stateless Streamable HTTP MCP endpoint at:

```text
https://<deployment-host>/mcp
```

The endpoint is anonymous and read-only. It exposes exactly these tools:

- `ioc_list_providers`
- `ioc_search_exact`
- `ioc_get_detail`
- `ioc_browse`
- `ioc_get_relations`

The tools use the same public IOC query semantics as the REST API. Exact search does not add fuzzy
matching or natural-language search. Tool responses include public IOC ids, provider record ids, page
metadata, and omission/truncation metadata when a finite response budget is reached.

## Quotas and proxy handling

MCP traffic has independent per-client request/concurrency limits and host-wide aggregate limits.
Excess traffic receives HTTP `429` with `Retry-After`. Client identity is derived from the direct
connection address unless the immediate peer is explicitly configured as a trusted proxy. Raw
forwarded headers from untrusted peers are ignored.

Configure accepted hosts and browser origins explicitly at the deployment boundary. CORS is not an
authentication mechanism. Multi-replica deployments must enforce the aggregate budget at the gateway
or a shared limiter in addition to these process-local safeguards.

Indicator URLs are returned as data and are never fetched by MCP.

## IOC chat

IOC chat is available at:

```text
POST /api/iocs/{id}/chat
```

It is anonymous but disabled by default. The selected IOC is resolved by the server. Conversation
history is bounded and remains only in React memory; refresh clears it. The server does not persist
messages, prompts, model output, or tool evidence.

Chat requires a separate Admin-configured OpenAI-compatible model endpoint with tool-call support.
The chat credential is independent from the AI report credential. If chat is disabled, unconfigured,
missing its credential, rate limited, cancelled, or the model does not support tools, the API fails
closed with a safe ProblemDetails response and does not answer from ungrounded model knowledge.

The assistant may answer only from public IOC MCP evidence. Facts and assessments are labeled, and
assessments must identify supporting IOC/provider records. User text and provider fields are treated as
untrusted data and cannot alter tool or disclosure boundaries. The assistant does not browse the web,
fetch indicator URLs, disclose source code/secrets/Admin data, or answer unrelated questions.

Visitors are informed that their message and selected IOC evidence are sent to the Admin-configured
model endpoint.

## Enable and rollback

1. Apply the chat settings migration.
2. In Admin settings, configure the chat Base URL and model ID.
3. Set the separate IOC chat API key through the write-only credential control.
4. Verify the model supports function/tool calls with the MCP/chat contract tests.
5. Enable IOC chat only after quota and privacy checks pass.

To roll back chat, disable its enabled flag in Admin settings. Public REST IOC browsing and AI report
generation remain independent. If the public MCP surface must be withdrawn, remove the `/mcp` route at
the API/ingress deployment and retain the disabled chat settings row.