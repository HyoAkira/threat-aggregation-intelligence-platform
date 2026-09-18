# ADR 0002: Model destination boundary

**Status:** Accepted — 2026-09-17

## Context

Generating a report sends a bounded IOC evidence projection to the Admin-configured OpenAI-compatible server. The user has chosen to permit any OpenAI-compatible endpoint, including local and self-hosted servers.

## Decision

An Admin may configure any absolute HTTP or HTTPS OpenAI-compatible endpoint. No deployment allowlist, private-address filter, loopback filter, or DNS-resolution filter is applied. This includes local, private-network, and container-service addresses such as `http://ollama:11434`.

## Dependent decisions

The backend still appends the fixed chat-completions operation path and applies response-size, timeout, and connection limits.
