## Why

Provider credentials are currently immutable `.env` options. That makes rotation, multi-host consistency, and write-only administration unsafe. The provider fetch contract also needs an explicit `requiresApiKey` policy so a missing key only blocks providers that require one.

## What Changes

- Add an allowlisted, database-backed credential store for ThreatFox, PhishTank, and OpenAI-compatible server metadata.
- Encrypt each credential directly with RFC 5116 AEAD_AES_256_GCM / NIST SP 800-38D AES-256-GCM using a deployment-managed 32-byte versioned root key, a fresh 12-byte nonce, a 16-byte tag, and AAD containing the provider/secret identity and format version.
- Add atomic write-only admin set/replace, clear, and status operations. The plaintext value is never returned, logged, placed in request-URI logs, or cached by the frontend.
- Add provider `requiresApiKey` settings. ThreatFox defaults to required; PhishTank defaults to optional and keeps its anonymous fetch behavior when no key is configured.
- Resolve credentials at each scheduled or manual fetch. A missing required key fails only that provider fetch and is recorded in job history; application startup remains healthy.
- Import legacy environment keys idempotently, without overwriting database credentials, then document removal of the legacy variables.
- Add version-aware root-key rotation/re-encryption, deployment secret documentation, EF migration, focused tests, and a runbook.

## Capabilities

### New Capabilities

- `encrypted-secret-management`: authenticated storage, resolution, migration, clearing, and root-key rotation for allowlisted external-service credentials.

### Modified Capabilities

- `admin-provider-management`: provider key policy and write-only credential status/mutation.
- `admin-frontend-runtime-settings`: keeps the full non-secret PUT snapshot free of plaintext keys and adds a separate write-only credential section under `/admin/settings`.
- `threatfox-client` and `phishtank-client`: operation-scoped credential resolution with required/optional behavior.

## Impact

The Domain/Application/Infrastructure layers gain the encrypted credential entity, contracts, AES-GCM service, repository, root-key configuration, EF migration, bootstrap importer, and fetch-resolution errors. API and frontend gain admin-only write-only endpoints and controls. Deployment keeps only the versioned root key and non-secret endpoint settings after migration.
