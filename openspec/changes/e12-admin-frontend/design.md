## Context

See proposal.md. The current frontend is a TanStack Start starter; E11 owns its shell, session-backed auth, typed RFC 7807 client, query-cache lifecycle, route guards, and browser-capable component-test setup. E12 consumes E04/E05/E10/E11. E10 must expose server-authoritative `defaultMode`, per-mode trigger states (`canTrigger`, `blockedReason`, `availableAt`), and `minimumIntervalMinutes`; deriving those values from history in the browser would duplicate guard logic and become stale.

## Goals / Non-Goals

**Goals:** Keep server state in one typed query layer, make mutations single-flight and race-aware, preserve useful stale data, render operational text safely, and make all Admin workflows keyboard and narrow-screen usable.

**Non-Goals:** No provider CRUD, secret or `.env` editing, client-owned scheduling decisions, retry/DLQ administration, live push channel, log export/search, production nginx work, or replacement for server authorization.

## Decisions

- Use file-based TanStack routes for `/admin/providers`, `/admin/summary`, `/admin/logs`, and `/admin/settings` under one E11 Admin layout/guard. This reuses session and forbidden handling; independent route guards were rejected because they would drift.
- Validate every E10 response and mutation body with Zod at the transport boundary. Query keys use `['admin', resource, ...parameters]`; mutations invalidate only affected provider, history, summary, or settings keys. Unvalidated casting was rejected because backend drift would silently corrupt operational UI.
- Poll provider state every 15 seconds only while visible and active jobs every 2 seconds by `jobId`; stop terminal polling and invalidate once. WebSockets/SSE were rejected for the MVP because E10 exposes bounded polling contracts and no push infrastructure.
- Treat API trigger state as authoritative. A local one-second countdown is display-only and forces a refetch at zero. Browser-derived guard math was rejected because modes use different histories/cooldowns and races still require server 409.
- Confirm every Full fetch with a Radix alert dialog, immediately acquire a per-provider single-flight mutation lock, and do not optimistically show Queued before 202. The API remains the cross-tab concurrency boundary.
- Keep provider/page history selection in validated URL search state. Keep form drafts, dialogs, countdown display, and log filters local; TanStack Query owns fetched state.
- Preserve the last successful response during background failures and label it stale. Operational continuity is more useful than blanking the view, provided freshness is explicit.
- Render logs and terminal errors only through React text nodes; prohibit HTML/Markdown interpretation. Long values wrap and open in an accessible detail dialog.
- Use semantic comparison tables at wide widths with equivalent labelled card/stack presentation at narrow widths. Status changes use non-disruptive accessible announcements and dialog focus is restored.

## Risks / Trade-offs

- [E10 contract is not yet sufficient for authoritative trigger/interval UI] → Revise E10 planning and implementation before E12 apply; keep the expected fields explicit in E12 specs.
- [E11 has not yet established proxy/CORS, hydration-safe session state, or browser tests] → Make E11 completion an apply prerequisite and reuse its facilities rather than creating E12-specific alternatives.
- [Client clock drift affects countdown text] → Show absolute UTC availability and always refetch before enabling.
- [Two Admins can overwrite settings] → Accept E10's atomic last-write-wins MVP behavior; refresh the returned baseline after every save.
- [Large log/error text affects rendering] → Keep E10 bounds, wrap content, and render detail on demand.

## Migration Plan

Land after E10 and E11. Add routes incrementally behind the Admin guard, then enable navigation after their focused tests pass. Rollback removes Admin routes and query modules without backend or data migration.
