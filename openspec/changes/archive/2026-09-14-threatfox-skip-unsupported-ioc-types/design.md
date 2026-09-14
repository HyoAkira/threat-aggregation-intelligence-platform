## Context

See `proposal.md` — Why. Today `ThreatFoxRecordMapper.Map` throws `ThreatFoxRecordParseException` for any `ioc_type` outside `domain`/`url`/`ip:port`, and `ThreatFoxFetchExecutor` converts that into a `TerminalFetchException`, failing the whole job. The mapper is invoked inside `ThreatFoxRecentParser`'s async iterator, which both the recent and full paths stream through. The platform's `IocType` covers only IPv4/IPv6/Domain/URL.

## Goals / Non-Goals

**Goals:**

- A ThreatFox export containing unsupported IOC types (file hashes today, unknown types tomorrow) ingests every supported record and completes.
- Skips are observable: one structured log summary per source, with counts broken down by raw `ioc_type`.
- Genuinely malformed records (blank id/ioc, invalid timestamps) keep their existing terminal, whole-batch-abort behavior.

**Non-Goals:**

- Ingesting file-hash IOCs (no `IocType` extension, no schema/classifier changes).
- Persisting skip counts on the job row or surfacing them in the API (log-only, per product decision).
- Changing PhishTank ingestion semantics (its malformed-record abort policy is untouched).

## Decisions

1. **Skip inside the parser pipeline, not a catch-and-continue in the executor.**
   `Map` is called from inside an `IAsyncEnumerable` iterator. Once the iterator throws, the enumerator is dead — the executor cannot resume the stream after catching, so executor-level recovery would require re-fetching and re-parsing the source. Filtering unsupported records where they are mapped is the only resumable point.
   *Alternative rejected:* a dedicated "unsupported type" exception subtype caught per record in the executor — impossible for the same iterator-termination reason.

2. **`Map` returns `ProviderRecordInput?`, where null means "unsupported IOC type, skip".**
   The parser is the only production caller, so a nullable return is a small, explicit contract. The null return is distinct from `DeclaredType: null` (the `ip:port` case, where the classifier decides the type).
   *Alternatives rejected:* a `Success/Skipped` result wrapper (over-engineered for one caller and one skip reason); keeping the throw and catching it in the parser per record (exceptions as per-record control flow, and a muddier mapper contract).

3. **The parser counts and logs skips; the mapper stays silent.**
   `ThreatFoxRecentParser` runs once per source (each recent kind, and the full export via `ThreatFoxFullParser`), so it accumulates skip counts keyed by the raw trimmed `ioc_type` string and emits one warning-level summary when enumeration ends, only when skips occurred. One line per source avoids per-record log spam — the full export can contain tens of thousands of hash IOCs. Normalization (`Trim` + `ToLowerInvariant`) is applied before the lookup, so `SHA256_HASH` skips identically.
   *Alternative rejected:* per-record warnings — unreadable on the full feed.

4. **Feed record indices keep counting every record, including skipped ones.**
   `recordIndex` still increments per parsed JSON record, so malformed-record failures continue to name the true feed position. The executor's per-source `RecordsParsed`/`RecordsAccepted` summary is unchanged — it already counts only what the executor consumes.

5. **Doc comments updated where they overpromise.**
   The mapper and parser summaries currently say unsupported/unmappable records are "never silently dropped"; they are narrowed to genuinely malformed records, and the skip behavior is documented in the parser summary.

## Risks / Trade-offs

- [Hash IOCs are dropped by design] → Mitigation: the per-source summary log names each skipped `ioc_type` with counts, so the data loss is visible and bounded to known-unsupported types.
- [A future ThreatFox type worth ingesting would be skipped without failing loudly] → Mitigation: the summary log enumerates every distinct skipped type; operators can alert on unexpected values there.
- [Log-only counting means no job-level skip metric] → Mitigation: structured log fields are greppable/alertable; job-row persistence is an explicit non-goal that can be added later without changing this design.
- [Behavioral test inversion] → The existing "unsupported ioc_type is reported with record context" test flips from asserting a throw to asserting a skip; intentional and covered by the new scenarios in the delta specs.

## Migration Plan

Pure code change — no schema, API, or config changes. Deploy and re-trigger the failed manual full fetch. Rollback is reverting the deploy; no data migration in either direction.
