## Why

The frontend currently ships through TanStack Start, producing an SSR server bundle even though all domain data is fetched from the API in the browser and authentication is stored in browser session storage. The deployment target needs a standalone static SPA artifact that can be served by a static web server while preserving every existing user and Admin workflow.

## What Changes

- Replace the TanStack Start runtime and Vite plugin with TanStack Router's standalone Vite integration while retaining the existing file-based route layout and type-safe generated route tree.
- Add the static HTML and client bootstrap required to mount the React application in the browser; move document-level metadata and styles out of Start's server document shell.
- **BREAKING:** stop producing and requiring the TanStack Start SSR server bundle. The production frontend artifact becomes static HTML, JavaScript, CSS, and assets only.
- Preserve client-side route guards, session restoration, API error handling, deep-link resume, route search state, code splitting, and all public and Admin functionality.
- Define the static hosting contract: same-origin `/api/*` proxying, history fallback for client routes, and 404 handling for missing asset-like requests. Coordinate this contract with the pending E14 frontend production container work rather than creating a second nginx design.
- Update development, production-build, unit, and browser-flow verification so the tested runtime is the static SPA.

## Capabilities

### New Capabilities

- `frontend-static-spa-runtime`: Builds and boots the file-based TanStack Router frontend as a static single-page application while preserving navigation, browser session behavior, and API integration.

### Modified Capabilities

- None.

## Impact

- Affects `frontend/package.json`, `frontend/vite.config.ts`, the root route, generated route configuration, a new client entry point and `index.html`, frontend tests, and frontend documentation.
- Removes the runtime dependency on `@tanstack/react-start`; adds the TanStack Router Vite plugin as a direct development dependency while retaining `@tanstack/react-router` and existing routes.
- Changes the production artifact consumed by the E14 frontend container from a Start server/client build to static files. The .NET API contract, authentication token format, API endpoints, and public/Admin route URLs remain unchanged.
