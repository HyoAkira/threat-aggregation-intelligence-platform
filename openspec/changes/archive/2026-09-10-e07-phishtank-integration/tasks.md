## 1. PhishTank adapter

- [x] 1.1 Implement the client with user agent, timeout, optional key, streamed `.json.gz`, and typed HTTP failures; verify request tests (TI-070).
- [x] 1.2 Stream the JSON within the memory bound; verify gzip, JSON, and record failures are terminal and safe (TI-071, TI-003).
- [x] 1.3 Map every record/detail field and keep detail IPs contextual; verify round-trip fixtures (TI-072).
- [x] 1.4 Make HTTP 429 fail once with zero counters/writes; verify the lifecycle test (TI-074).
- [x] 1.5 Run a full fixture through E05/E08 and verify counters, idempotency, and bad-final-record rollback (TI-075).
