## Context

See proposal.md. E04 supplies Admin authorization, E05 owns fetch lifecycle/history, E09 defines grouped query semantics and ProblemDetails, and E02 supplies provider/settings/history persistence. E10 adds read and control surfaces without changing the schema.

## Goals / Non-Goals

**Goals:** Keep Admin APIs thin, reuse existing lifecycle/grouping rules, apply settings atomically, and expose bounded secret-safe diagnostics.

**Non-Goals:** No `.env` reads or writes, secret display, arbitrary file access, admin frontend, or new fetch execution path.

## Decisions

- Use MediatR handlers with Infrastructure readers/writers; controllers only authorize, validate, and map responses.
- Share E09 pagination and ProblemDetails contracts so Admin endpoints do not invent another envelope or error format.
- Apply a settings request in one database transaction, then invalidate short-TTL caches. The scheduler recomputes due time from the last successful completion and never schedules a disabled or never-run provider.
- Read logs from configured rolling-file paths only. Parse newest files first, skip malformed lines, cap results before returning, and never accept a caller-supplied path.
- Emit structured lifecycle events at the E05 lifecycle boundary so API, broker, and Worker activity uses the same `jobId` and correlation fields.

## Risks / Trade-offs

- [Settings caches briefly disagree] → Use a 30-second maximum TTL and explicit invalidation after commit.
- [Log rotation occurs during a read] → Snapshot candidate files, tolerate missing/partial files, and return valid entries collected so far.
- [Summary queries become expensive] → Count indexed distinct normalized groups and cover representative SQL plans in E13.

## Migration Plan

Deploy additively after E04/E05/E09. Rollback removes the endpoints and event hooks; persisted settings and history remain compatible.
