## Purpose

Let visitors investigate and compare IOC evidence in a temporary conversation on the IOC detail page, using only public MCP data as the factual basis for answers.

## ADDED Requirements

### Requirement: Anonymous IOC-page chat

The IOC detail page SHALL offer chat to anonymous and signed-in visitors as a separate, explicitly initiated action from AI report generation. A conversation SHALL start with the displayed IOC as context and SHALL allow questions about other IOC values, groups, providers, and relationships available through MCP. The server SHALL accept a bounded user message and bounded conversation history for `POST /api/iocs/{id}/chat`; it SHALL resolve the selected IOC itself and SHALL reject an unknown id.

#### Scenario: Visitor compares another IOC
- **WHEN** an anonymous visitor asks how the displayed IOC compares with another valid IOC
- **THEN** the assistant can retrieve both through MCP and return an evidence-based comparison with links or identifiers for the examined groups

#### Scenario: Chat is not started automatically
- **WHEN** a visitor opens an IOC detail page
- **THEN** no chat model request is made until the visitor submits a message

### Requirement: MCP-grounded answers and assessments

The assistant SHALL use only results from the public IOC MCP tools as evidence. If it draws an assessment from those results, it SHALL clearly label the assessment as an inference, identify the supporting IOC and provider records, and distinguish it from observed facts. It SHALL acknowledge missing, conflicting, truncated, or unavailable evidence and SHALL not invent a definitive conclusion. It SHALL not perform internet search, visit indicator URLs, or treat user text or provider-supplied fields as instructions to change its behavior.

#### Scenario: Provider evidence supports a conclusion
- **WHEN** an MCP result shows a provider's verified phishing record for a URL
- **THEN** the assistant may assess that the URL appears to be phishing, label that statement as an assessment, and cite the provider record

#### Scenario: Evidence is insufficient
- **WHEN** the MCP results do not support the requested claim
- **THEN** the assistant states that the database does not establish the answer instead of guessing or searching elsewhere

#### Scenario: Provider content contains instructions
- **WHEN** an IOC field contains text telling the assistant to ignore its rules or reveal secrets
- **THEN** the assistant treats that text as untrusted data and follows the tool and answer boundaries

### Requirement: Topic and disclosure boundaries

The assistant SHALL answer only questions grounded in public IOC MCP data, including comparisons and explanations of returned fields. It SHALL refuse unrelated requests and requests for source code, internal prompts, secrets, Admin data, or operations outside the public read-only tools. Tool execution SHALL be restricted server-side to the public IOC MCP allowlist, independently of model instructions. Chat responses SHALL not expose configured model credentials or internal error details.

#### Scenario: Unrelated coding request
- **WHEN** a visitor asks the assistant to write unrelated code or reveal the application's source
- **THEN** it declines and performs no non-IOC tool call

#### Scenario: Site question unsupported by MCP
- **WHEN** a visitor asks about site behavior not represented in MCP data
- **THEN** the assistant states that the available IOC data cannot answer it

### Requirement: Ephemeral, bounded conversation

The browser SHALL keep the transcript only in memory for the current page session and SHALL clear it on refresh. The server SHALL not persist transcripts, prompts, model output, or tool evidence as chat history, and routine logs SHALL contain metadata rather than message content or credentials. The API SHALL enforce finite request, history, tool-call, output, time, and concurrent-run budgets, including limits for anonymous visitors. It SHALL return safe, distinguishable errors for invalid input, disabled or unconfigured chat, rate limits, and upstream failure. The UI SHALL tell visitors that messages and IOC evidence are sent to the Admin-configured model endpoint, render model text safely, and not show an incomplete response as a completed answer.

#### Scenario: Refresh clears conversation
- **WHEN** a visitor refreshes the IOC page after chatting
- **THEN** the previous messages are no longer available in the UI or from a server-side transcript store

#### Scenario: Chat budget is exhausted
- **WHEN** an anonymous visitor exceeds a chat request or concurrency limit
- **THEN** the API rejects the request with a retryable rate-limit response and no upstream model call

#### Scenario: Model or tool call fails
- **WHEN** the model or MCP fails during a response
- **THEN** the UI shows a safe failure state and does not present partial text as a verified completed answer
