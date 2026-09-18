## Why

The public IOC API exposes useful threat data, but external MCP clients cannot consume it as a defined tool set and visitors cannot ask follow-up questions or compare indicators on the IOC page. A public, read-only MCP surface and a tightly scoped chat experience will make the existing evidence easier to investigate without exposing administrative functions or creating saved conversations.

## What Changes

- Expose the existing anonymous IOC provider, exact search, detail, browse, and relation queries as bounded, read-only MCP tools for website and non-website clients. Do not expose AI report generation, mutations, or Admin APIs through MCP; do not add fuzzy or natural-language search.
- Add anonymous IOC-page chat that starts with the displayed indicator and may query or compare other indicators through MCP. The assistant may make clearly labeled assessments supported by returned evidence, cite the relevant IOC/provider records, and state when the database cannot support an answer. It does not search external sources or answer unrelated requests.
- Add separate Admin settings for the chat model's Base URL, model ID, API key, and enabled state, independent of the existing AI report configuration. Chat requires a model endpoint that supports tool calling.
- Keep conversation state in browser memory only and clear it on refresh. Add server-side request, tool-use, response-size, and concurrency limits for anonymous MCP and chat use.

## Capabilities

### New Capabilities

- `public-ioc-mcp`: Anonymous, read-only MCP transport and bounded IOC tools backed by the public query behavior.
- `ioc-chat`: Anonymous IOC-page conversation, cross-IOC investigation, evidence-grounded assessments, refusal boundaries, ephemeral history, and chat limits.
- `ioc-chat-configuration`: Admin-only chat model settings and write-only credential management, independent of report settings.

### Modified Capabilities

- `auth`: Add the public MCP and chat endpoints to the explicit anonymous-access requirements while retaining Admin protection.
- `admin-frontend-runtime-settings`: Add a separate chat AI settings section to the Admin settings experience without changing the existing runtime-settings save contract.

## Impact

The .NET API gains a Streamable HTTP MCP endpoint, tool-call-capable chat orchestration, separate chat settings and secret identity, and abuse controls. The React IOC detail page gains chat; the Admin settings page gains chat model controls. Existing public IOC query semantics remain the source of truth, and the existing AI report endpoint stays outside MCP. No server-side conversation store or external enrichment is introduced.
