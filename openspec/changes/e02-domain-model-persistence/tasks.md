## 1. Domain contracts

- [ ] 1.1 Add the documented byte-backed enum ordinals and verify an enum contract test covers every name and value (TI-020)
- [ ] 1.2 Harden `Provider` construction and verify tests reject blank name/base URL, undefined status, and intervals below one minute (TI-020)
- [ ] 1.3 Harden `IoC` required identity fields and verify tests reject missing provider, blank/overlength provider-record ID, blank/overlength IOC value, and non-32-byte hash (TI-020)
- [ ] 1.4 Harden `IoC` port and confidence ranges and verify boundary tests preserve valid `0` confidence while rejecting invalid values (TI-020)
- [ ] 1.5 Harden `IoC` type and timestamp fields and verify tests reject undefined IOC types and non-UTC timestamps (TI-020)
- [ ] 1.6 Harden `FetchHistory` construction and verify tests cover job ID, provider, enum values, UTC times, non-negative counters, and terminal-state consistency (TI-020)
- [ ] 1.7 Harden `User` construction and verify tests cover username/password-hash lengths, role validity, and UTC `createdAt` (TI-020)
- [ ] 1.8 Harden `Settings` construction and verify tests cover key length, required value, and UTC `updatedAt` (TI-020)
- [ ] 1.9 Normalize and validate `Tag` names and verify tests prove trimming, lowercase storage, blank rejection, and the 100-character limit (TI-020)
- [ ] 1.10 Harden `IoCTag` construction and verify tests reject non-positive IOC and tag identities (TI-020)

## 2. Identity hashing

- [ ] 2.1 Define the normalized-value hashing abstraction in Application and verify project dependency rules still pass (TI-026)
- [ ] 2.2 Implement SHA-256 over UTF-8 normalized values in Infrastructure and verify fixed vectors produce exactly 32 bytes (TI-026)
- [ ] 2.3 Implement post-hash normalized-value comparison and verify tests distinguish an equal value from a simulated hash collision (TI-026)

## 3. DbContext and entity mappings

- [ ] 3.1 Create and register `ThreatIntelligenceDbContext` and verify API and Worker service providers resolve it (TI-021)
- [ ] 3.2 Map `Provider` and verify its table, columns, types, lengths, nullability, and enum conversion match Technical Specification §4 (TI-021)
- [ ] 3.3 Map `IoC` and verify its table, columns, types, lengths, nullability, UTC values, and enum conversion match Technical Specification §4 (TI-021)
- [ ] 3.4 Map `Tag` and verify its table, name length, and required normalized name match Technical Specification §4 (TI-021)
- [ ] 3.5 Map `IoCTag` and verify the join table has the documented composite primary key (TI-021)
- [ ] 3.6 Map `FetchHistory` and verify its columns, types, nullability, counters, UTC values, and enum conversions match Technical Specification §4 (TI-021)
- [ ] 3.7 Map `User` and verify its columns, lengths, role conversion, and UTC `createdAt` match Technical Specification §4 (TI-021)
- [ ] 3.8 Map `Settings` and verify its columns, lengths, nullability, and UTC `updatedAt` match Technical Specification §4 (TI-021)
- [ ] 3.9 Configure Provider-to-IOC and verify the expected foreign key exists (TI-021)
- [ ] 3.10 Configure Provider-to-FetchHistory and verify the expected foreign key exists (TI-021)

## 4. Database constraints

- [ ] 4.1 Add the `portKey = COALESCE(port, 0)` persisted computed column and verify its generated SQL uses `0` only for NULL ports (TI-022)
- [ ] 4.2 Add the unique endpoint-identity index using `portKey` and verify SQL-backed tests reject duplicate NULL and equal ports while allowing different ports (TI-022)
- [ ] 4.3 Add the unique `(providerId, providerRecordId)` index and verify a SQL-backed duplicate insert fails (TI-022)
- [ ] 4.4 Add the unique `fetchHistory.jobId` index and verify a SQL-backed duplicate insert fails (TI-022)
- [ ] 4.5 Add the unique `users.username` index and verify a SQL-backed duplicate insert fails (TI-022)
- [ ] 4.6 Add the unique `settings.key` index and verify a SQL-backed duplicate insert fails (TI-022)
- [ ] 4.7 Add the unique `tags.name` index and verify a SQL-backed duplicate insert fails (TI-022)
- [ ] 4.8 Add the port CHECK constraint and verify SQL-backed boundary tests accept 1/65535 and reject 0/65536 (TI-022)
- [ ] 4.9 Add the ThreatFox confidence CHECK constraint and verify SQL-backed boundary tests accept 0/100 and reject out-of-range values (TI-022)
- [ ] 4.10 Add the PhishTank JSON CHECK constraint and verify SQL-backed tests accept valid JSON and reject malformed JSON (TI-022)

## 5. Supporting indexes

- [ ] 5.1 Add `(iocType, iocValueHash)` and verify it exists in generated schema metadata (TI-022)
- [ ] 5.2 Add `(iocType, urlHost)` and verify it exists in generated schema metadata (TI-022)
- [ ] 5.3 Add `(iocType, parentDomain)` and verify it exists in generated schema metadata (TI-022)
- [ ] 5.4 Add the `lastSeenUtc` ordering index and verify it exists in generated schema metadata (TI-022)
- [ ] 5.5 Add `(providerId, startedAt)` on fetch history and verify it exists in generated schema metadata (TI-022)

## 6. Migration and provider seeds

- [ ] 6.1 Generate the `InitialCreate` migration and verify it applies to an empty SQL Server database (TI-023)
- [ ] 6.2 Reapply the migration set to an already-migrated database and verify no data, schema object, or migration row is duplicated (TI-023)
- [ ] 6.3 Seed ThreatFox with ID 1, its documented base URL, enabled state, 5-minute interval, and NULL `nextFetchTime`, and verify the exact row in SQL Server (TI-023)
- [ ] 6.4 Seed PhishTank with ID 2, its documented base URL, enabled state, 8-hour interval, and NULL `nextFetchTime`, and verify the exact row in SQL Server (TI-023)
- [ ] 6.5 Make provider seeding insert-if-absent and verify reseeding preserves operator changes without adding rows (TI-023)
- [ ] 6.6 Run migrations and provider seeds from both hosts and verify a clean Compose start initializes once and a restart adds no duplicates (TI-023)

## 7. First-admin seed preparation

- [ ] 7.1 Define the password-hasher abstraction required by first-admin seeding and verify E02 contains no production KDF implementation (TI-024)
- [ ] 7.2 Implement the first-admin seed workflow against the abstraction and verify a test hasher produces one Admin with the configured username and UTC `createdAt` (TI-024)
- [ ] 7.3 Prevent plaintext persistence and logging and verify a capture test finds the configured password in neither database values nor emitted logs (TI-024)
- [ ] 7.4 Make repeat seeding non-destructive and verify an existing Admin's row, role, and password hash remain unchanged (TI-024)
- [ ] 7.5 Reject an existing same-name non-Admin as a configuration conflict and verify the user remains unchanged (TI-024)
- [ ] 7.6 Handle concurrent seed attempts and verify SQL-backed execution leaves exactly one matching Admin or reports a conflicting winner (TI-024)
- [ ] 7.7 Reject missing or invalid seed configuration and verify no user row is created (TI-024)
- [ ] 7.8 Reject an unavailable password hasher and verify no user row is created (TI-024)

## 8. Repository abstractions

- [ ] 8.1 Define endpoint-identity lookup and verify its contract includes provider, IOC type, hash, and nullable port (TI-025)
- [ ] 8.2 Implement endpoint-identity lookup and verify SQL-backed tests cover NULL port, equal port, and different-port candidates (TI-025)
- [ ] 8.3 Define and implement provider-record-ID lookup and verify found and not-found SQL-backed cases (TI-025)
- [ ] 8.4 Implement exact grouped reads for one `(iocType, normalizedValue)` and verify hash candidates are value-compared while all provider/port source rows are returned (TI-025)
- [ ] 8.5 Define and implement fetch-history persistence and verify a history row can be created and updated independently (TI-025)

## 9. Transaction boundary

- [ ] 9.1 Define the IOC unit-of-work contract and verify it exposes begin, commit, and rollback operations (TI-025)
- [ ] 9.2 Implement IOC transaction commit and verify all staged IOC/tag changes persist together (TI-025)
- [ ] 9.3 Implement IOC transaction rollback and verify no staged IOC/tag change persists after failure (TI-025)
- [ ] 9.4 Persist fetch history outside the IOC transaction and verify a rolled-back IOC batch retains one Failed history row with zero counters and safe error detail (TI-025)

## 10. Change verification

- [ ] 10.1 Run `dotnet build` and the complete unit and integration test suites and verify they pass without secret-bearing output
