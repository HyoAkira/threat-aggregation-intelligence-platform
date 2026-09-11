## Context

See proposal.md. The existing frontend is a TanStack Start/Vite React shell with TanStack Router, TanStack Query, Zustand, Zod, Tailwind, and Radix, but it has only a starter route and in-memory auth state. E11 consumes E04 login and E09 IOC query contracts.

## Goals / Non-Goals

**Goals:** Accessible responsive user routes, one typed API/error layer, safe session cleanup, and URL-driven browse state.

**Non-Goals:** Admin workflow pages, server-side authorization replacement, offline mode, token refresh, or backend contract changes.

## Decisions

- Use file-based TanStack Router routes for `/login`, `/search`, `/iocs/$id`, and `/browse`; route guards remember the requested URL and enforce role-aware navigation.
- Keep the JWT in `sessionStorage`, mirrored in Zustand for rendering. Clear it and the TanStack Query cache on logout, expiry, or 401. Do not store passwords or tokens in URLs or logs.
- Use one API client for Bearer injection and RFC 9457 parsing. Surface correlation IDs on unexpected errors.
- Use TanStack Query for request state and pagination; query keys include every filter. Browse controls serialize to validated URL search parameters.
- Use shared presentation components for group summaries and source cards so search, detail, relations, and browse preserve the same provider attribution.
- Use Radix primitives and semantic HTML for keyboard/focus behavior; visual tokens live in the existing stylesheet/Tailwind theme.

## Risks / Trade-offs

- [Browser session storage is exposed to injected script] → Keep dependencies audited, avoid unsafe HTML, apply CSP during packaging, and clear sessions aggressively.
- [Backend contracts change before E09 lands] → Validate responses with Zod and keep transport DTOs isolated from presentation components.
- [Large relation sets affect rendering] → Fetch and render one bounded API page at a time.

## Migration Plan

Replace the starter route incrementally, keeping the development `/api` proxy. Rollback restores the starter shell; no server or data migration is required.
