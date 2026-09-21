## 1. Failing tests — mapper and parser skip semantics

- [x] 1.1 In `ThreatFoxRecordMapperTests`, replace the throw-asserting unsupported-type coverage: `Map` returns null for `sha256_hash`, `md5_hash`, an unknown type, and a mixed-case variant; supported types (`domain`, `url`, `ip:port`) still map. Verify with `dotnet test tests/ThreatIntelligence.UnitTests --filter ThreatFoxRecordMapperTests` failing (current code throws).
- [x] 1.2 In `ThreatFoxRecentParserTests`, invert `Unsupported_ioc_type_is_reported_with_record_context` to assert skip semantics (no exception, valid records still yielded). Verify the filtered run fails.
- [x] 1.3 In `ThreatFoxRecentParserTests`, add cases: unsupported record between valid records is skipped in both root shapes (array and keyed object); a feed of only unsupported records yields nothing and does not throw; a malformed record after a skipped one still throws with a feed index that counts the skipped record. Verify the filtered run fails.

## 2. Failing tests — full parser and executor integration

- [x] 2.1 In `ThreatFoxFullParserTests`, add a zip whose `full.json` contains a `sha256_hash` record between domain records; assert every domain record is produced and no exception is thrown. Verify the filtered run fails.
- [x] 2.2 In `ThreatFoxFetchExecutorTests`, add a full-mode test whose feed contains a `sha256_hash` record between valid records; assert the job succeeds and the supported records are committed (requires local SQL Server on localhost:1432 with `TEST_SQLSERVER_PASSWORD` in `.env`). Verify the filtered run fails.

## 3. Implementation

- [x] 3.1 `ThreatFoxRecordMapper`: `Map` returns `ProviderRecordInput?` — null for unsupported `ioc_type` (after trim/lowercase normalization); genuinely malformed records (null record, blank id/ioc, invalid timestamps) still throw `ThreatFoxRecordParseException`; update the class doc comment. Verify mapper tests pass.
- [x] 3.2 `ThreatFoxRecentParser`: skip null-mapped records instead of yielding, keep incrementing the feed record index for them, accumulate skip counts by raw `ioc_type`, and emit one warning-level summary at the end of each `ParseAsync` enumeration when any skips occurred; narrow the class doc comment ("never silently dropped" now applies to malformed records only). Verify recent-parser tests pass.

## 4. Verification

- [x] 4.1 Run the full unit suite (`dotnet test tests/ThreatIntelligence.UnitTests`) and the ThreatFox integration tests (`dotnet test tests/ThreatIntelligence.IntegrationTests --filter ThreatFox`); confirm all green, including the pre-existing malformed-record abort tests.
- [x] 4.2 Run `dotnet build` across the solution (and the repo's format/lint command if one is configured) and fix any warnings introduced by the nullable `Map` signature.
