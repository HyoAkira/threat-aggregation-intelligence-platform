# IOC AI Report — Configuration and Operations

On-demand, source-cited Turkish reports generated from stored IOC evidence through an
OpenAI-compatible chat-completions server. Reports are ephemeral: the API writes neither report
text nor evidence snapshots to the database, cache, or logs.

## Capabilities and endpoints

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/admin/ai/settings` | Admin | Non-secret connection settings plus `apiKeyConfigured` status |
| `PUT /api/admin/ai/settings` | Admin | Atomic save of `{baseUrl, modelId, enabled}` |
| `GET /api/admin/ai/report-policy` | Admin | Active instruction version + full history |
| `PUT /api/admin/ai/report-policy` | Admin | Publishes a new immutable version (`text`, `expectedVersion`; 409 on conflict) |
| `PUT/DELETE /api/admin/credentials/OpenAICompatible.ServerApiKey` | Admin | Write-only API key set/replace/clear |
| `GET /api/admin/credentials/OpenAICompatible.ServerApiKey` | Admin | Configured status only (never plaintext) |
| `POST /api/iocs/{id}/ai/reports` | Everyone (anonymous, `User`, or `Admin`) | Streams `meta`/`delta`/`complete`/`error` SSE events; request body is empty |

Existing anonymous IOC browsing is unchanged. The Generate report action and endpoint are
available to everyone, including anonymous visitors; server-side rate and concurrency limits
still apply.

## Configuration

1. Apply migrations (additive tables only: `aiConnectionSettings`, `aiReportPolicyVersions`,
   `aiReportPolicyActive`).
2. Deploy the encryption root key (`ENCRYPTION__ROOTKEY` or `ENCRYPTION__ROOTKEYFILE`, exactly
   32 bytes base64, with `ENCRYPTION__KEYVERSION`). Without it the credential store is
   unavailable and AI generation fails closed with `ai_credential_unavailable`.
3. Sign in as an Admin and configure in `/admin/settings` → **AI analysis** → **AI Connection**:
   - Base URL: any absolute `http`/`https` OpenAI-compatible address (public, private-network,
     loopback, or container-service names). Example: `http://127.0.0.1:3002`.
   - Model ID: the chat-completions model name understood by that server, e.g. `llama3.1`.
   - Enable AI analysis and save. The optional server API key is set through the write-only
     credential control; keyless local servers need no key.
4. The default Turkish instruction document seeds as version 1 on first startup when no policy
   exists. Edit and publish new versions in the Admin panel; restore earlier versions by loading
   them into the editor and publishing them as a new version.

### Local-model container networking

`localhost` and `127.0.0.1` inside the API container resolve to the API container itself. When
the model service runs in another container or on the host, use its reachable service address,
for example `http://ollama:11434` (compose network) or the host's IP. The server applies no
outbound destination allowlist or address filter; the configured endpoint is trusted by
configuration.

## Supported chat-completions subset

The adapter calls `POST <baseUrl>/chat/completions` (the fixed operation path is appended; the
Base URL is normalized once). The request uses:

```json
{
  "model": "<modelId>",
  "messages": [
    {"role": "system", "content": "<report policy (versioned)>" },
    {"role": "system", "content": "<fixed safety rules>" },
    {"role": "user", "content": "<bounded IOC evidence snapshot JSON>"}
  ],
  "stream": true,
  "temperature": 0.2,
  "max_tokens": 4000
}
```

The response must be `text/event-stream` with `data: {"choices":[{"delta":{"content":...}}]}`
frames and a final `data: [DONE]`. Responses that deviate (non-SSE lines, malformed JSON) map to
a stable `ai_unsupported_upstream_response` error. The API key, when configured, is resolved from
the encrypted secret store immediately before the operation and is only placed in the upstream
`Authorization: Bearer` header; it is never logged or cached. The browser sends no JSON payload
with the report POST; the server loads the IOC evidence and policy itself. Authentication is
optional for this public generation endpoint.

## What is sent upstream

For one generation run, the model receives:

- The active report instruction text (version-pinned).
- Fixed code-owned safety instructions (never Admin-editable).
- One bounded JSON snapshot of the selected IOC group: normalized value/type, aggregate times,
  and per-source entries `S1..Sn` with provider, provider record id, times, tags, false-positive
  state, and relevant typed fields. Raw `PhishTankDetails` JSON is projected to at most 12
  bounded lines; provider detail links, reporter identity, and unrelated IOC rows are excluded.
  Deterministic truncation applies (max 24 sources / 32 KB) and the omitted-source count is
  disclosed in the report.

The server discards the snapshot when the request completes. Report text is never persisted or
routinely logged.

## Prompt-version workflow

1. The Admin edits the instruction text in `/admin/settings` → **AI analysis** → **Report Instructions**.
2. Publishing validates non-empty text, a 20,000-character maximum, and `expectedVersion`
   (optimistic concurrency). Stale publishes receive HTTP 409 `report_policy_conflict` and the
   draft is retained.
3. Each published version is immutable; restoration republishes prior content as a new version.
4. A report pins the active version at generation start, and the version is displayed with the
   completed report.

## Limits

| Limit | Value |
| --- | --- |
| Per-user report requests | 10 per sliding 10-minute window |
| Per-user concurrent runs | 1 |
| Host-wide concurrent runs | 4 (per process) |
| Evidence snapshot | 24 sources / 32 KB, deterministic truncation |
| Report output | 24,000 characters; `max_tokens` 4000 |
| Upstream timeout | 5 minutes total per streaming run |
| Credential length | 4,096 characters |

Quota exhaustion returns HTTP 429 with the stable code `ai_quota_exceeded` and a
`Retry-After` header. Concurrent overflow is refused before any upstream request is made.

## Error contract

Pre-stream failures are ProblemDetails (`application/problem+json`); after streaming begins, a
safe `error` SSE event terminates the run and `complete` is never emitted after `error`:

- `ai_disabled` — feature disabled (HTTP 409)
- `ai_not_configured` — missing settings/policy/IOC (HTTP 409; IOC-not-found uses the existing
  404 semantics)
- `ai_invalid_configuration` — invalid Base URL (HTTP 400)
- `ai_credential_unavailable` — configured credential cannot be resolved (HTTP 503)
- `ai_upstream_failure` / `ai_upstream_timeout` / `ai_unsupported_upstream_response` — streamed
  as `error` events
- `ai_output_invalid` — the model output failed section/source-label validation
- `ai_quota_exceeded` — 429

## Observability

Logs record IOC id, authenticated username, model id, policy version, duration, status, and
correlation id only. Prompt bodies, IOC snapshots, report text, API keys, Authorization headers,
and upstream error bodies are absent from logs (covered by unit log-capture tests).

## Rollback

1. Disable AI generation first (`enabled=false` in Admin AI settings) — the API then refuses
   generation with `ai_disabled` and ordinary IOC browsing is unaffected.
2. Revert the application deployment. The additive AI tables remain; they contain no secrets
   and can be ignored. Removing the schema is a separately reviewed data-retention operation.
3. Remove the stored server key through the credential DELETE endpoint if the deployment is
   retired entirely.

## Deployment checklist

1. Migration applied; default policy version 1 seeded.
2. Root key deployed and verified through the credential status endpoint.
3. Admin saves Base URL (`http://127.0.0.1:3002` for a local stub/model), model ID, and enables
   AI.
4. Controlled end-to-end generation run with a stub or configured model.
5. Verify: anonymous browsing unchanged, anonymous and signed-in generation, mid-stream failure handling,
   429 behavior, zero secret logging, and nginx SSE flush/timeout settings
   (`proxy_buffering off;` and `proxy_read_timeout` above the upstream timeout) for the
   `/api/iocs/*/ai/*` routes.
