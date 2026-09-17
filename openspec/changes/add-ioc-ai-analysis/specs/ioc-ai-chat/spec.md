## Purpose

Analistin tamamlanmış bir IOC raporu hakkında soru sormasını sağlar; konuşmanın kanıt ve erişim kapsamını raporun tek IOC anlık görüntüsüyle sınırlar.

## ADDED Requirements

### Requirement: Owner-bound report conversation

Chat SHALL be available only for a completed report to its authenticated owner. Each request SHALL identify the IOC and report and contain one bounded user message; clients SHALL not submit role-labeled history, alternate report text, or evidence. The server SHALL maintain canonical conversation history, include at most the last eight messages in a model call, and reject missing, foreign, incomplete, or mismatched reports.

#### Scenario: Owner asks a follow-up
- **WHEN** the report owner asks about a completed IOC report
- **THEN** the answer is generated with that report, its frozen evidence, and the bounded server-side conversation history

#### Scenario: Client substitutes another report
- **WHEN** a user submits a report id belonging to another user or IOC
- **THEN** the request is denied without loading that report into the model context

### Requirement: Restricted answer scope

The model SHALL be given no tools, no general database access, and no access to external IOC URLs. The chat instructions SHALL ask for answers grounded in the report and frozen source labels and for a concise refusal when the question cannot be answered from that context. User messages and provider text SHALL be treated as untrusted data. Message length, history length, output length, duration, rate, and concurrency SHALL be bounded.

#### Scenario: Prompt injection in source data
- **WHEN** a provider detail or user question contains an instruction to ignore the analysis rules or reveal secrets
- **THEN** the server does not add tools or secrets to the model context and the answer remains subject to the same scope and output checks

#### Scenario: Unrelated question
- **WHEN** a user asks for information unrelated to the selected IOC or report
- **THEN** the chat returns a scoped refusal rather than presenting an unrelated answer as IOC evidence

### Requirement: Chat stream and safe history

Chat SHALL stream provisional answer text with the same event and error semantics as report generation. Only successfully completed user/assistant turns SHALL enter the canonical history. The UI SHALL keep partial failed answers visibly incomplete and SHALL render model text without raw HTML or executable links.

#### Scenario: Chat generation is interrupted
- **WHEN** the user cancels or the upstream stream fails
- **THEN** the turn is not committed as a completed assistant answer and the user can retry deliberately

