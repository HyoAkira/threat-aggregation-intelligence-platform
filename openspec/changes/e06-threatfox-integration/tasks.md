## 1. ThreatFox adapter

- [ ] 1.1 Implement the secret-safe client for three recent CSV endpoints and full CSV ZIP; verify URLs, timeout, streaming, cancellation, and redaction (TI-060).
- [ ] 1.2 Stream and validate recent domain, URL, and IP-port records; verify fixtures and malformed-row context (TI-061).
- [ ] 1.3 Stream the full ZIP and verify missing-entry, corrupt, truncated, and archive-bomb tests (TI-062).
- [ ] 1.4 Map every documented field and tag; verify `false`, `0`, empty, and null fidelity (TI-063).
- [ ] 1.5 Register the validator and verify cooldown/error classification through E05 (TI-064).
- [ ] 1.6 Run recent/full fixtures through E08 and verify one commit or complete rollback (TI-065).
