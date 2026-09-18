# ADR 0001: Role of an AI report

**Status:** Accepted — 2026-09-17

## Context

The change promises a Turkish, source-cited assessment. The initial design proposed completed-report storage and report-bound chat, which would create history, ownership, retention, and access-control requirements.

## Decision

An AI report is a one-time, downloadable analyst aid. Anyone may request one from the IOC detail page. The server streams and validates it but does not save report text, source snapshots, or report history. The browser displays the validated result for the current page session and offers a local download. Reloading or leaving the page discards it.

## Dependent decisions

Server-side ownership, report sharing, retention, historical IOC identity, freshness comparisons, and report-bound chat are removed from the change. The report remains subject to structural validation and an analyst-review disclaimer.
