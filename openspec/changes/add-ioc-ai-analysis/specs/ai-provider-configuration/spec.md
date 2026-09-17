## Purpose

Yöneticinin IOC analizi için kullanılacak OpenAI uyumlu sunucuyu güvenli biçimde seçmesini ve gizli anahtarın durumunu değeri ifşa edilmeden görmesini sağlar.

## ADDED Requirements

### Requirement: Admin-only AI connection settings

`GET /api/admin/ai/settings` and `PUT /api/admin/ai/settings` SHALL require the Admin role. The non-secret settings SHALL include `baseUrl`, `modelId`, and an `enabled` flag; the response SHALL expose only whether `OpenAICompatible.ServerApiKey` is configured, never its plaintext value. API key set, replace, and clear operations SHALL use the existing write-only credential contract, separate from the settings PUT.

#### Scenario: Admin configures a model
- **WHEN** an Admin saves a valid Base URL and model ID and enables AI analysis
- **THEN** a subsequent settings read returns the persisted non-secret values and only credential status metadata

#### Scenario: Non-Admin attempts configuration
- **WHEN** an anonymous or non-Admin caller reads or changes AI settings
- **THEN** the API returns 401 or 403 and no configuration changes

### Requirement: OpenAI-compatible endpoint configuration

The server SHALL reject an empty model ID when AI analysis is enabled and SHALL require an absolute HTTP or HTTPS Base URL. An Admin MAY configure any OpenAI-compatible endpoint, including public, private-network, loopback, container-service, and plain-HTTP addresses. The server SHALL append the fixed chat-completions operation path and SHALL not apply an outbound destination allowlist or address filter. Invalid updates SHALL preserve the previous settings.

#### Scenario: Admin configures a local model
- **WHEN** an Admin supplies `http://ollama:11434` and a model ID
- **THEN** the configuration is accepted and report generation calls that OpenAI-compatible server

### Requirement: Missing or unavailable AI configuration fails closed

AI report generation SHALL not contact an upstream model while the feature is disabled, its connection settings are incomplete, or a required configured credential cannot be resolved. The API SHALL return a stable non-secret error; ordinary IOC browsing SHALL remain available.

#### Scenario: Credential store is unavailable
- **WHEN** the configured AI connection requires a stored key and that key cannot be resolved
- **THEN** generation fails before an upstream request and the response contains no key material
