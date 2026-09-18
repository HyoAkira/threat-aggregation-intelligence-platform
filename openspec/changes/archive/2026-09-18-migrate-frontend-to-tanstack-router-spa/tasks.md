## 1. Establish the standalone Router build

- [x] 1.1 Record the pre-migration frontend verification baseline by running the current unit, build, and browser suites; capture any pre-existing route-file warnings separately so only migration regressions block completion.
- [x] 1.2 Replace the direct TanStack Start dependency with the direct TanStack Router Vite plugin dependency, update the lockfile using the repository package manager, and verify the resolved package graph contains no direct `@tanstack/react-start` dependency.
- [x] 1.3 Replace the Start Vite plugin with the documented `tanstackRouter` Vite integration before the React plugin while preserving Tailwind, devtools, the development `/api` proxy, and file-based route generation; verify `npm run generate-routes` and `npm run build` regenerate a type-correct route tree.
- [x] 1.4 Update package scripts and frontend documentation to describe development, static build output, static preview, same-origin `/api` proxy expectations, and client-route fallback; verify the documented commands work from a clean dependency install.

## 2. Transfer startup and document ownership to the browser

- [x] 2.1 Add `frontend/index.html` with the existing language, viewport, title, font resources, and a `#root` mount element; verify the production output retains those head resources and the root mount point.
- [x] 2.2 Add a browser-only `src/main.tsx` that imports global CSS, creates the existing configured router, and mounts `RouterProvider` with React `createRoot`; verify a static preview loads the home route without a frontend SSR process.
- [x] 2.3 Refactor `src/routes/__root.tsx` from Start's document shell into an ordinary root route with the Query client provider, app shell, router outlet, and existing devtools behavior; verify the visible shell, global styles, and route outlet render identically at `/`.
- [x] 2.4 Regenerate `src/routeTree.gen.ts` under the Router Vite plugin, remove Start-specific generated registration, retain Router type registration, and verify TypeScript/router type checks report no Start imports or Start `ssr` configuration dependencies.
- [x] 2.5 Remove the now-inapplicable Admin `ssr: false` setting and revise its commentary to describe browser session guards without SSR; verify a valid session still permits direct Admin entry and a missing or non-Admin session still follows the existing guard behavior.

## 3. Preserve behavior and static-host integration boundaries

- [x] 3.1 Update test render helpers and DOM-test setup that depend on Start `shellComponent` or server-render terminology so they mount the Router SPA root; verify existing shell, route-guard, search, browse, detail, and Admin unit tests continue to pass.
- [x] 3.2 Add static-runtime browser coverage that opens `/browse?type=url&pageSize=10` directly, verifies the existing normalized API request and rendered data, and exercises in-app navigation/back-forward URL state against the production static preview.
- [x] 3.3 Add browser coverage using a pre-seeded valid `sessionStorage` Admin session before first navigation to `/admin/providers`, and coverage for an absent session; verify the former sends its bearer-authenticated protected request and the latter redirects through the existing login flow without rendering Admin data.
- [x] 3.4 Verify the static artifact and hosting contract with automated checks: the build emits `dist/index.html` and assets but no Start server bundle; known client routes receive the app entry document; and nonexistent asset-like paths return 404. Keep concrete nginx proxy/cache/security-header implementation in E14 while confirming its expected `/api/*` and fallback interface has not changed.

## 4. Complete quality gates and release hand-off

- [x] 4.1 Run `npm run check`, `npm run lint`, `npm test`, `npm run build`, and `npm run test:e2e` from `frontend`; verify all pass and test the built static preview rather than a development server.
- [x] 4.2 Inspect the final build and source tree for removed Start runtime usage, static entry/document assets, unchanged public/Admin URLs, and relative `/api/*` calls; verify no backend API, JWT/session schema, or CORS change is included.
- [x] 4.3 Document the E14 implementation hand-off: the frontend container must serve the static build directory, proxy `/api/*` same-origin, apply client-route fallback only to non-file routes, and preserve missing-asset 404 behavior; verify the hand-off references the `frontend-static-spa-runtime` requirements.
