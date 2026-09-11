## 1. Atomic pipeline

- [x] 1.1 Resolve native and normalized identities, confirm hashes by value, and reject conflicts; verify identity tests (TI-080).
- [x] 1.2 Upsert allowed fields and exact counters while preserving creation/first-seen; verify repeat-fetch tests (TI-081, TI-004).
- [x] 1.3 Replace per-source tags with a cross-chunk scalar ID cache; verify removal, reuse, and attribution tests (TI-082).
- [x] 1.4 Process ~2,000-row chunks in one outer transaction; verify a bad record rolls back every IOC/tag write (TI-083, FR-009).
- [x] 1.5 Make retries and concurrent redelivery equivalent to one delivery; verify no duplicates or false counters (TI-084).
- [x] 1.6 Calculate false-positive state at the exact 180-day boundary; verify reappearance clears it (TI-085).
- [x] 1.7 Verify the full unit and SQL integration suite passes, including TI-138 scenarios.
