## 1. OpenSpec and API contract

- [x] 1.1 Add the `ioc-tag-catalog` capability delta and align controller/reader contracts with existing anonymous IOC query behavior.
- [x] 1.2 Add the bounded `GET /api/iocs/tags` action and typed catalog item.
- [x] 1.3 Implement the EF tag usage aggregation, normalized prefix filter, deterministic ordering, and limit application without a migration.

## 2. Verification

- [x] 2.1 Add integration coverage for anonymous access, counts, ordering, prefix normalization, empty results, and invalid limits.
- [x] 2.2 Add frontend schema/client/query-key coverage and a shared autocomplete input used by browse and search.
- [x] 2.3 Run targeted/full tests, validate the OpenSpec change, update task checkboxes, and run `graphify update .`.
