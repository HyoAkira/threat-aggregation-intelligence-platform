# ADR 0007: Default generation limits

**Status:** Pending user decision (grilling round 3)

## Context

Report generation calls a model endpoint on demand. Limits protect responsiveness and prevent accidental bursts, but they also affect large IOC groups and users who need several reports quickly.

## Decision to make

Choose whether to use the proposed initial defaults: one active report per user, two reports per user per minute, a 90-second upstream timeout, 64 KiB request-scoped evidence, and a 24 KiB completed report. These values can be exposed as deployment configuration later.

## Dependent decisions

Whether limits are fixed or configurable, and the user-facing `429`/timeout messages, follow from this choice.
