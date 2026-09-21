## Purpose

Harici servis credential'larını SQL Server'da doğrudan AES-256-GCM ile doğrulanabilir ciphertext olarak saklar, yalnızca desteklenen entegrasyonlara çözer ve operasyonel rotasyonu güvenli kılar.

## ADDED Requirements

### Requirement: Direct AES-256-GCM encrypted storage

The system SHALL persist each managed credential only as AES-256-GCM authenticated ciphertext using a deployment-managed 32-byte root key. Each write SHALL generate a unique cryptographically random 12-byte nonce and store a 16-byte authentication tag, format version, secret identity, key version, ciphertext, and UTC audit timestamps. The AAD SHALL include the format version and canonical secret identity.

#### Scenario: Secret is stored

- **WHEN** an administrator creates or replaces a managed secret
- **THEN** the database contains no submitted plaintext, the ciphertext metadata is complete, and a direct record read cannot recover the value without the external root key

#### Scenario: Ciphertext is tampered with

- **WHEN** ciphertext, nonce, tag, key version, format version, or identity is modified
- **THEN** resolution fails closed without returning plaintext or making an upstream request

### Requirement: Secret identity allowlist

The system SHALL accept only the typed identities `ThreatFox.ApiKey`, `PhishTank.AppKey`, and `OpenAICompatible.ServerApiKey`. It SHALL reject database, broker, JWT, administrator, and arbitrary secret names without changing storage.

#### Scenario: Unsupported identity is rejected

- **WHEN** a credential operation uses an identity outside the supported allowlist
- **THEN** the operation returns a validation error and does not create or modify a database row

### Requirement: Secret-safe resolution

The system SHALL resolve a credential only inside the authorized integration operation, keep plaintext in memory for the shortest practical scope, and exclude it from responses, exceptions, telemetry, request-URI logs, and frontend query caches.

#### Scenario: Provider resolution does not expose plaintext

- **WHEN** an authorized provider fetch resolves a configured credential
- **THEN** the value is passed only to the provider client and is absent from API responses, logs, job history, exceptions, and query-cache data

### Requirement: Idempotent legacy import

The system SHALL map legacy environment names explicitly and insert only when the target row is absent. Repeated import SHALL preserve an existing database value, including a value previously rotated by an administrator, and SHALL report only non-sensitive status.

#### Scenario: Existing credential is preserved during import

- **WHEN** startup import runs and a managed credential row already exists
- **THEN** the importer leaves that row unchanged and reports only whether the target is configured

### Requirement: External root-key configuration and rotation

The system SHALL obtain the 32-byte root key and positive key version from deployment-managed secret configuration outside SQL Server. Missing or invalid key material SHALL disable only secret-management initialization and SHALL NOT prevent the host from starting for unrelated providers. Root-key rotation SHALL support mixed-version reads and resumable transactional re-encryption while retaining old keys until old ciphertext and backups are retired.

#### Scenario: Root key is unavailable

- **WHEN** the application starts without a valid root key
- **THEN** no credential is read or written and the host remains available for unrelated runtime functionality

### Requirement: Nonce and usage limits

The system SHALL never reuse a nonce with the same key version and SHALL document and test the per-key usage limit and rotation-before-limit procedure. Replacing a credential SHALL create a new nonce and SHALL not be treated as root-key rotation.

#### Scenario: Replacement uses a fresh nonce

- **WHEN** an administrator replaces an existing credential under the same root-key version
- **THEN** the new ciphertext has a newly generated nonce and authentication tag
