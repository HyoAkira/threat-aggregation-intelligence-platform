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

### Requirement: Safe connection target and configuration validation

The server SHALL reject an empty model ID when AI analysis is enabled and SHALL reject a Base URL with credentials, query, fragment, unsupported scheme, or an unapproved destination. Public destinations SHALL require HTTPS; private, loopback, or plain-HTTP destinations SHALL require an explicit deployment-controlled allowlist entry. Validation SHALL be enforced again when making the outbound request, including resolved addresses and redirects. Invalid updates SHALL preserve the previous settings.

#### Scenario: Dangerous URL is rejected
- **WHEN** an Admin submits a metadata-service, loopback, or private-network URL that has not been explicitly allowed by deployment configuration
- **THEN** the API returns 400 and makes no outbound request

#### Scenario: Local model is explicitly allowed
- **WHEN** an Admin selects an allowed local HTTP origin and a model ID
- **THEN** the configuration is accepted and inference requests are restricted to that configured origin

### Requirement: Missing or unavailable AI configuration fails closed

AI report and chat generation SHALL not contact an upstream model while the feature is disabled, its connection settings are incomplete, or a required configured credential cannot be resolved. The API SHALL return a stable non-secret error; ordinary IOC browsing SHALL remain available.

#### Scenario: Credential store is unavailable
- **WHEN** the configured AI connection requires a stored key and that key cannot be resolved
- **THEN** generation fails before an upstream request and the response contains no key material

