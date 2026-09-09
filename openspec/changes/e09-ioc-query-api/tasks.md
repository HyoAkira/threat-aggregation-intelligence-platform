## 1. IOC query API

- [ ] 1.1 Define the grouped source-attributed DTO and page envelope; verify golden JSON contracts (TI-090).
- [ ] 1.2 Implement exact normalized search with port semantics and optional source filters; verify 400/404/grouping cases (TI-091).
- [ ] 1.3 Implement normalized tag filtering without losing source attribution; verify SQL-backed cases (TI-092).
- [ ] 1.4 Implement detail-by-row-ID with complete sources and MIN/MAX dates; verify 400/404/group cases (TI-093).
- [ ] 1.5 Implement paginated `urlHost` and `parentDomain` relations; verify bounded apex, subdomain, and IP cases (TI-094).
- [ ] 1.6 Implement filtered grouped browse with deterministic null/tie ordering and pageSize ≤ 100; verify stable pages (TI-095).
- [ ] 1.7 Implement safe RFC 7807 errors with correlation IDs and remove the scaffold controller; verify every status mapping (TI-096).
- [ ] 1.8 Verify the clean build and full unit/SQL integration suite passes with no schema or `.env` change.
