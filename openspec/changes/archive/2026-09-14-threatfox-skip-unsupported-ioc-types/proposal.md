## Why

A manual ThreatFox full fetch (job `d9646290-c91e-403a-b3a1-d74338cf3467`, 2026-09-11) failed at record 508 with zero records written: the full export contains file-hash IOCs (`sha256_hash`, also `md5_hash`/`sha1_hash`), which the platform cannot represent (`IocType` covers only IPv4/IPv6/Domain/URL), and the mapper's unsupported-type parse exception aborts the whole batch. Any ThreatFox export containing a single hash IOC can never complete.

## What Changes

- ThreatFox records with an unsupported `ioc_type` (anything other than `domain`, `url`, `ip:port`) are skipped instead of aborting the fetch job.
- `ThreatFoxRecordMapper.Map` returns a skip signal (null) for unsupported IOC types instead of throwing `ThreatFoxRecordParseException`; `ThreatFoxRecentParser` filters those records out of its stream.
- The parser logs a warning summary per source with skip counts broken down by `ioc_type` (log-only; no DB/API contract change).
- Malformed records (blank id/ioc, invalid timestamps), feed-shape errors, and archive errors still abort the batch — unchanged.
- Hash IOCs remain unrepresented; ingesting them would be a future capability (not this change).

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `threatfox-recent-ingestion`: Unsupported IOC types are no longer treated as malformed records that abort E08's batch; they are skipped with a logged, counted summary. The "Malformed record handling" requirement is narrowed to genuinely malformed records (blank id/ioc, invalid timestamps).
- `threatfox-full-ingestion`: Full-export ingestion completes past records with unsupported IOC types; the "Malformed JSON record" requirement is narrowed the same way.

## Impact

- `src/ThreatIntelligence.Infrastructure/ThreatFox/ThreatFoxRecordMapper.cs` — `MapIocType`/`Map` skip semantics.
- `src/ThreatIntelligence.Infrastructure/ThreatFox/ThreatFoxRecentParser.cs` — skip filtering and per-source skip-count logging.
- `ThreatFoxFetchExecutor`, validators, batch writer, DB schema — no changes (parse-exception terminal path remains for genuinely malformed records).
- Tests: `ThreatFoxRecordMapperTests`, `ThreatFoxRecentParserTests`, `ThreatFoxFullParserTests` (unit); `ThreatFoxFetchExecutorTests` (integration — full-mode job containing hash records now succeeds).
