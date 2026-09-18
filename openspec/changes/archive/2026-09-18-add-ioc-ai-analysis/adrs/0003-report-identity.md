# ADR 0003: Durable identity of a report's IOC

**Status:** Superseded — 2026-09-17

## Context

The current IOC detail URL uses a provider row ID to find a group keyed by normalized `(type, value)`. The group displays its lowest row ID. A row can later move to another normalized identity through provider upsert, and rows can disappear.

## Outcome

The accepted ephemeral-report decision removes this issue: the API resolves the IOC group at generation time only and retains no report that needs historical lookup.

## Dependent decisions

If persistent report history is introduced later, this decision must be revisited.
