# E14 Frontend Container Hand-off

This document is the artifact hand-off from
`migrate-frontend-to-tanstack-router-spa` to the E14
`frontend-production-container` work (E14 tasks 1.1-1.3). The frontend no longer
produces a TanStack Start SSR bundle; `npm run build` emits a static SPA under
`frontend/dist/` (`index.html` plus hashed assets).

## Artifact

- Build directory: `frontend/dist/` containing `index.html` and `assets/*.js`
  and `assets/*.css`.
- There is no `dist/server` directory and no server entry for application
  rendering.
- Entry document: `frontend/index.html`; browser bootstrap: `frontend/src/main.tsx`.

## Host requirements (owned by E14)

The frontend container must satisfy the `frontend-static-spa-runtime` capability:

1. Serve the static build directory as the document root
   (`Static browser application artifact`).
2. Proxy `/api/*` to the API on the same origin. The browser must never see a
   cross-origin API URL; the frontend keeps relative `/api/*` calls
   (`Same-origin API and session behavior are retained`).
3. Apply client-route fallback only to non-file routes so `/`, `/browse`,
   `/search`, `/login`, `/admin/*`, and `/iocs/*` return the entry document
   (`Existing routes remain browser-navigable`).
4. Preserve 404 for missing file-like asset paths instead of returning the entry
   document (`Static hosting distinguishes client routes from missing assets`).

These match E14 task 1.3 (nginx `/api` proxying, SPA fallback, missing-asset 404
behavior) and do not change the API contract, JWT/session format, route URLs, or
CORS policy.

## Verification

`frontend/e2e/static-host.contract.spec.ts` locks this interface with a test
double and `frontend/README.md` documents the same contract for operators.
