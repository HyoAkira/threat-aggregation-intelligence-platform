## Context

See proposal.md for motivation and `frontend-static-spa-runtime` for the behavioral contract. The current `frontend` package uses `@tanstack/react-start` and `tanstackStart()` in Vite. A production build emits both `dist/client` and `dist/server/server.js`. There is no application `index.html` or client entry because Start owns document generation and hydration.

The project already uses `@tanstack/react-router` file routes and the generated `src/routeTree.gen.ts`. `src/router.tsx` already creates the router with preload and scroll-restoration settings, restores the browser-only session, and registers the 401 redirect handler. All business data is requested in the browser through relative `/api/*` calls in `src/lib/apiClient.ts`; the repository contains no `createServerFn` calls or Start server route handlers. `/admin` is explicitly `ssr: false` because its session is browser session storage.

`src/routes/__root.tsx` is the sole Start document dependency: it uses `shellComponent`, `HeadContent`, and `Scripts` to create `<html>`, metadata, font links, stylesheet links, providers, and the app shell. The current test harness includes SSR-oriented naming and a test-only `shellComponent` override. E14 already specifies the nginx production concerns for `/api` proxying, SPA fallback, caching, headers, and `/healthz`; this change supplies the static artifact that E14 will serve.

### Fact checks from official TanStack documentation (2026-09-16)

- TanStack Router documents Vite file-based routing with the direct `@tanstack/router-plugin` dependency. It requires `tanstackRouter(...)` to be placed before the React Vite plugin and generates `src/routeTree.gen.ts` by default. Source: [Installation with Vite](https://tanstack.com/router/latest/docs/installation/with-vite).
- TanStack Router's manual setup specifies a browser `src/main.tsx`, a root element in `index.html`, `createRouter({ routeTree })`, `RouterProvider`, and declaration merging to retain router type safety. Source: [Manual Setup](https://tanstack.com/router/latest/docs/installation/manual).
- A file-based router consumes the generated `routeTree` through `createRouter`; type registration is required for router-wide TypeScript inference. Source: [Creating a Router](https://tanstack.com/router/latest/docs/guide/creating-a-router).
- TanStack Start can alternatively prerender static pages, but its automatic discovery excludes parameterized routes unless values are supplied or linked. Since this application serves live API data at parameterized IOC URLs and the objective is to remove the Start server runtime, prerendering is not the chosen deployment model. Source: [Static Prerendering](https://tanstack.com/start/latest/docs/framework/react/guide/static-prerendering).

## Goals / Non-Goals

**Goals:** Preserve every current route URL and frontend behavior while producing a conventional Vite static SPA; retain file-based routing and router type safety; keep API traffic same-origin; make the production build itself the target of automated verification; leave a clear artifact boundary for E14's nginx image.

**Non-Goals:** Change API endpoints, JWT/session format, authorization rules, page UI, data-fetching semantics, URL schemas, or backend CORS policy; add static prerendering or SEO snapshots; design or implement E14's container, proxy, cache, and security-header configuration; migrate the application to a different router or build tool.

## Decisions

- Replace Start with standalone TanStack Router plus Vite, not with a different routing library. The application already uses the Router route APIs, generated tree, links, guards, search validation, and route-level lazy code. Retaining these minimizes behavioral risk. Rewriting routes is rejected as unnecessary scope.

- Add `@tanstack/router-plugin` as a direct development dependency and replace `tanstackStart()` with `tanstackRouter({ target: 'react', autoCodeSplitting: true })` before `@vitejs/plugin-react`. Keep Tailwind and development tooling. This follows the official integration and preserves automatic route-tree generation. Continuing to call the router CLI alone is an alternative, but it leaves generation configuration split between scripts and Vite without a need in this project.

- Add root-level `frontend/index.html` with static document metadata, font links, and `<div id="root">`; add `src/main.tsx` that imports global CSS, obtains the existing router, and mounts `<RouterProvider>` with React's client `createRoot`. This transfers document ownership from Start to Vite. A server-generated document is rejected because no SSR runtime will remain.

- Convert `__root.tsx` from a document `shellComponent` to an ordinary root-route component containing the Query client provider, `AppShell`, router outlet, and development-only devtools. Move only static head data to `index.html`. Preserve the current page tree and providers; do not alter individual page components. This eliminates Start's `HeadContent`/`Scripts` contract without changing application content.

- Retain `getRouter()` as the single router factory. Remove only Start-specific generated declarations, let the Router Vite plugin regenerate `routeTree.gen.ts`, and retain the Router `Register` declaration. Session hydration remains during browser router creation, before the application is rendered, so direct Admin loads keep their present behavior.

- Preserve the relative `/api` client base. The deployment contract requires same-origin reverse proxying because the API currently exposes no CORS policy and the client injects bearer tokens into relative requests. E14's frontend production container owns the concrete nginx rules; this change will document and integration-test the hand-off rather than duplicate container work.

- Test a build output that contains static `index.html` and no Start server output; use browser tests against the static preview for root and deep-link navigation, public browse behavior, login, stored-session Admin entry, and missing-asset behavior. Update unit test helpers only where they depend on Start's document shell. Retaining SSR render tests as the primary application harness is rejected because they would validate a runtime that is being removed.

## Risks / Trade-offs

- [The first page is no longer server-rendered] → Every current data screen already uses client-side queries; verify loading, error, and authenticated direct-load states in the static runtime.
- [A host without history fallback turns direct routes into 404s] → Encode and test the static host contract; coordinate the concrete nginx implementation with E14.
- [A cross-origin static host would fail API calls or require broader CORS changes] → Keep `/api/*` same-origin and proxy internally; do not introduce a browser-visible API origin.
- [Moving head/style ownership can cause missing fonts, CSS, or incorrect page title] → Assert emitted `index.html` includes the existing metadata and font links, and smoke-test styled page rendering.
- [Generated route tree changes can create noisy diffs] → Generate once with the Router Vite plugin, commit its intentional output, and verify no Start declarations remain.
- [Existing test helpers implicitly assume SSR] → Replace only the Start-specific shell assumptions and retain component contract tests; add browser coverage for startup and deep linking.
- [E14 is an active, unimplemented change] → Keep this change limited to the frontend artifact and contract; implementation must either sequence before E14's frontend image or be coordinated with it so its nginx rules point to the static build directory.

## Migration Plan

1. Record the current behavior with the existing unit and browser suites, especially login/deep-link, public IOC routes, Admin authorization, browse URL state, and direct URL loads.
2. Make the Router/Vite dependency and configuration switch; add the static entry document and browser mount; refactor only the root route and generated declarations needed to remove Start.
3. Build the application and inspect the output for `index.html`, hashed assets, absence of `dist/server`, and preservation of the route tree. Start the static preview.
4. Run unit, lint/type, and Playwright checks against the static preview. Add focused checks for direct `/browse` and stored-session `/admin/providers` loads, API request path preservation, and a missing asset 404.
5. Hand the verified build directory and host requirements to E14's frontend production container implementation. Verify its `/api` proxy and SPA fallback when that change is applied.

Rollback: restore the previous frontend lockfile, Vite configuration, root document component, and Start dependency as one commit; redeploy the previous Start-compatible frontend image or service. Because API routes and data contracts do not change, no backend or database rollback is needed.
