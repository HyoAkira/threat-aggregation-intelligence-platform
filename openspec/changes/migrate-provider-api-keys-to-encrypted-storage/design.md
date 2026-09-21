## Context

The current API and Worker bind `ThreatFox:ApiKey` and optional `PhishTank:AppKey` into immutable options during startup. Provider rows and runtime settings are persisted through EF Core and fetch history records terminal provider failures. The admin settings PUT is a complete non-secret snapshot and must not become a credential transport.

## Goals / Non-Goals

**Goals:**

- Store only authenticated ciphertext for supported external-service credentials.
- Use direct AES-256-GCM with a 32-byte deployment root key; no envelope/DEK wrapping is introduced in this change.
- Resolve the current credential in the operation scope of scheduled and manual provider fetches.
- Make `requiresApiKey` explicit, with ThreatFox required by default and PhishTank optional by default.
- Provide idempotent legacy import, write-only admin operations, resumable root-key rotation, and secret-free diagnostics.

**Non-Goals:**

- Moving database, broker, JWT, admin password, or other infrastructure secrets into this store.
- Implementing the OpenAI-compatible inference client. Only its allowlisted identity/metadata contract is added.
- Returning, exporting, masking, or recovering plaintext credentials through the API or UI.

## Decisions

### Direct AES-256-GCM record format

Every replacement generates a cryptographically random 12-byte nonce and encrypts the UTF-8 credential with a 32-byte root key selected by `keyVersion`. The record stores `formatVersion`, logical `secretName`, `keyVersion`, ciphertext, nonce, 16-byte authentication tag, and UTC audit timestamps. AAD is the canonical tuple `(formatVersion, secretName)` so ciphertext cannot be reassigned to another identity. The plaintext is never stored in a `Settings` value or provider column.

The implementation uses `System.Security.Cryptography.AesGcm` with a 16-byte tag. Nonce uniqueness is guaranteed by fresh CSPRNG generation for each write; the design never reuses a nonce/key pair. The operational limit is the NIST SP 800-38D per-key usage bound, with rotation scheduled before the deployment's configured write/byte budget is approached. Credential replacement is not root-key rotation.

### External versioned root key

`Encryption:RootKey` is supplied by a Docker Compose secret or equivalent deployment secret file and is decoded to exactly 32 bytes. `Encryption:KeyVersion` is a non-secret positive version. Missing or malformed root-key configuration prevents the secret-management capability from initializing, but does not stop the API/Worker host or unrelated providers from starting. A root-key rotation deploys a new version, re-encrypts records transactionally/resumably, retains the old key until all old records and backups are retired, and never emits plaintext.

### Allowlist and operation-scoped resolver

Typed identities are limited to `ThreatFox.ApiKey`, `PhishTank.AppKey`, and `OpenAICompatible.ServerApiKey`. Infrastructure/JWT/admin credentials are rejected. The resolver decrypts only inside a fetch or admin write operation; no singleton plaintext cache is used. Required-key absence raises a non-sensitive configuration exception before any upstream request. Optional-key absence passes `null` to the provider client, preserving anonymous PhishTank behavior.

### Provider policy and admin contract

Provider records gain `RequiresApiKey`; seeded defaults are ThreatFox `true` and PhishTank `false`. The ordinary provider/settings DTOs expose only the boolean and configured status. Credential set/replace and clear are separate admin endpoints, accept a value only in the request body, and return metadata (`configured`, `updatedAt`, `keyVersion`) only. The full settings PUT remains plaintext-key-free.

### Bootstrap migration

After EF migration and seeding, a scoped importer maps legacy environment names explicitly and inserts only when the corresponding secret row is absent. Database values always win, so repeated startup cannot overwrite an admin rotation. Startup diagnostics report only identity and configured/ skipped status.

## Risks / Trade-offs

- Root-key compromise exposes managed credentials; deployment secret permissions and rotation are the boundary.
- SQL backups contain ciphertext but cannot decrypt without the matching versioned root key; old backup keys must be retained until the backup retention window expires.
- Direct AES-GCM has a strict nonce/key usage limit; the runbook tracks writes/bytes per key version and rotates before the NIST bound is approached.
- A credential rotation racing a fetch is safe: the fetch resolves once and may finish with the prior value; the next operation sees the replacement.
- Missing required credentials fail an individual fetch and preserve job history; they do not fail host startup.

## Migration Plan

1. Deploy the schema, code, and a valid versioned root secret while legacy provider variables remain present.
2. Run the idempotent importer and inspect only configured-status results.
3. Execute controlled ThreatFox and PhishTank fetches, including the anonymous PhishTank path.
4. Remove legacy provider variables after verification; keep only the external root key and non-secret endpoints.
5. For root-key rotation, deploy old+new key material, re-encrypt mixed-version rows in resumable batches, verify zero old-version rows, then retire the old key only after backups using it expire.

## Open Questions

The concrete production secret-file mount differs by deployment; the configuration contract accepts Docker Compose secrets or an equivalent file-backed provider without changing the application behavior.
