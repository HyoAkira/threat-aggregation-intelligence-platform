# Provider credential migration and rotation runbook

## Secret boundary

`Encryption:RootKeyFile` must point to a deployment-managed secret file containing base64 for exactly 32 random bytes. `Encryption:KeyVersion` is a positive non-secret version. The file is mounted outside the repository and outside SQL Server. Docker Compose uses `/run/secrets/threatintel-encryption-root-key`; an equivalent host secret store may supply the same configuration contract.

The database stores only AES-256-GCM ciphertext, a 12-byte nonce, a 16-byte tag, format version, logical identity, key version, and UTC timestamps. Associated data is the format version plus the canonical identity. Every credential replacement generates a new nonce. A replacement is not a root-key rotation.

Never put credential values in response bodies, exception details, telemetry, request URLs, frontend query data, or support logs. Inspect only configured status, key version, ciphertext length/metadata, and job history.

## Initial deployment and legacy import

1. Create the root-key file with 32 bytes from a CSPRNG, encode it as base64, restrict file permissions, and record the deployment key version.
2. Deploy API and Worker with the migration code and the legacy `THREATFOX__APIKEY` / optional `PHISHTANK__APPKEY` variables still available. Do not print them.
3. Allow startup to apply the EF migration and run the idempotent importer. It inserts only missing identities: `ThreatFox.ApiKey` and `PhishTank.AppKey`.
4. Verify only `configured`, `updatedAt`, and `keyVersion` through the admin status endpoint. Confirm the seeded provider policies: ThreatFox requires a key; PhishTank does not.
5. Run one controlled ThreatFox fetch and one PhishTank fetch. A missing ThreatFox key must create only a failed provider job; a missing PhishTank key must use its anonymous feed mode.
6. Remove legacy provider variables from API/Worker environments after verification. Keep the root-key file and versioned configuration.

The importer is insert-if-absent. Re-running it cannot overwrite an administrator rotation.

## Root-key rotation

Rotate before the AES-GCM per-key usage bound is approached. Track the number and total plaintext bytes of writes per key version; nonce uniqueness is per key version and every write uses a fresh CSPRNG nonce.

1. Generate a new 32-byte root key and assign the next positive `KeyVersion`.
2. Deploy the new key as the active key and provide the old key in the versioned `Encryption:PreviousKeys` configuration. Keep both keys available to API and Worker.
3. Run the resumable `ISecretRotationService.ReencryptAsync` operation in bounded maintenance batches/controlled retries. It reads old-version ciphertext, authenticates/decrypts in memory, and transactionally replaces it with a new nonce and the active version.
4. Verify the remaining old-version count is zero and run controlled fetches. Do not export decrypted values.
5. Retain the old key until all database backups containing old-version ciphertext have expired, then remove it from deployment configuration.

If rotation is interrupted, rerun it; already re-encrypted rows are skipped by key version. If an old key is unavailable or authentication fails, stop and restore the correct versioned deployment secret rather than bypassing authentication.

## Rollback and incident response

- Before legacy variables are removed, rollback the application binaries while retaining the encrypted table and the root secret.
- After legacy variables are removed, restore the previous binary only if it supports the encrypted store; otherwise restore the provider values through the deployment secret process and do not write plaintext to SQL Server.
- A tamper/decryption failure is fail-closed. Disable the affected provider fetch, preserve the failed job history, and investigate the database row/key version without copying ciphertext or credentials into tickets.
- Root-key compromise requires immediate deployment-secret rotation, re-encryption, audit review, and retirement of the compromised key after backup retention.
