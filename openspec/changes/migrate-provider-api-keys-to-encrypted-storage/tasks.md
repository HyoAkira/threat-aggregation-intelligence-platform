## 1. Secret contract and cryptography

- [ ] 1.1 Define the allowlisted identities and `requiresApiKey` policy contracts for ThreatFox, PhishTank, and OpenAI-compatible server credentials; reject unsupported infrastructure names.
- [ ] 1.2 Add external versioned root-key configuration with exactly 32-byte validation; keep host startup available when secret management is unavailable.
- [ ] 1.3 Implement direct AES-256-GCM with 12-byte nonce, 16-byte tag, AAD `(formatVersion, secretIdentity)`, key versioning, nonce uniqueness, and usage-limit documentation/tests.
- [ ] 1.4 Ensure sensitive buffers and resolved plaintext have the shortest practical lifetime and are absent from exceptions, logs, telemetry, URLs, and frontend caches.

## 2. Persistence and runtime resolution

- [ ] 2.1 Add the encrypted-secret entity, repository/contracts, and SQL Server mapping with unique identity, binary metadata, UTC timestamps, and concurrency protection.
- [ ] 2.2 Create and validate the EF migration without dropping provider, IOC, fetch-history, or settings data.
- [ ] 2.3 Implement atomic create/replace/clear operations that preserve prior ciphertext on validation or encryption failure.
- [ ] 2.4 Implement scoped resolution with database precedence, missing/tampered errors, optional anonymous behavior, and no plaintext cache.
- [ ] 2.5 Register services in API and Worker DI and validate root-key configuration without making unrelated hosts fail at startup.

## 3. Bootstrap migration and provider clients

- [ ] 3.1 Implement explicit idempotent legacy environment import with insert-if-absent semantics and secret-free status logging.
- [ ] 3.2 Replace immutable ThreatFox/PhishTank option binding with operation-scoped resolution; preserve request behavior and PhishTank anonymous fetches.
- [ ] 3.3 Classify missing/undecryptable required credentials as terminal per-provider fetch failures and preserve job history without upstream calls.
- [ ] 3.4 Add the OpenAI-compatible identity and non-secret endpoint metadata contract; inference client remains out of scope.
- [ ] 3.5 Preserve key-free request logging and diagnostics.

## 4. Admin API and frontend behavior

- [ ] 4.1 Add Admin-only write-only credential set/replace, clear, and status endpoints with strict identity/length validation and atomic replacement.
- [ ] 4.2 Extend provider/settings projections with `requiresApiKey` and non-sensitive configured metadata only; keep full settings PUT plaintext-key-free.
- [ ] 4.3 Enforce authorization, request redaction, and safe errors for credential endpoints.
- [ ] 4.4 Add `/admin/settings` write-only credential controls with accessible validation, dirty-state safety, and no plaintext in rendered output or query cache.

## 5. Operations, documentation, and rotation

- [ ] 5.1 Add resumable root-key rotation/re-encryption using per-record key versions and transactional replacement.
- [ ] 5.2 Update `.env.example`, Docker Compose/deployment configuration, and startup documentation to use an external versioned root secret.
- [ ] 5.3 Add a runbook covering import order, verification, legacy-variable removal, backup/key retention, nonce/usage limits, rotation, and rollback.

## 6. End-to-end verification

- [ ] 6.1 Run focused unit/integration tests for crypto, persistence, migration, provider fetches, admin authorization, and secret-free logging.
- [ ] 6.2 Run backend/frontend suites and `openspec validate --strict`.
- [ ] 6.3 Execute controlled provider fetches after bootstrap and rotation, inspecting only ciphertext/status/job history and confirming zero plaintext disclosure.
