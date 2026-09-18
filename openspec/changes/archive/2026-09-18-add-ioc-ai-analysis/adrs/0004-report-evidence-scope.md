# ADR 0004: Evidence scope for a generated report

**Status:** Accepted — 2026-09-17

## Context

The example report begins by retrieving a live ThreatFox record. The earlier proposal and design prohibit IOC URL retrieval and external enrichment, and use the stored IOC group as the complete report input. These choices create materially different security, reliability, privacy, and provenance behaviour.

## Decision

Generate from the data already ingested into this platform. The report identifies its data as the platform's current stored provider evidence. It does not retrieve IOC URLs, refresh ThreatFox records, or perform other external enrichment during generation.

## Dependent decisions

The report has no external-provider availability, credential, or request-time provenance dependency. Source labels refer to the stored provider-attributed evidence projection.
