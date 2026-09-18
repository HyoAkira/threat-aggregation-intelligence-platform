## Purpose

Delivers the existing frontend as a static browser application without a frontend SSR process while retaining its route, API, and authentication behavior.

## ADDED Requirements

### Requirement: Static browser application artifact
The frontend build SHALL produce a deployable static entry document and browser assets, and SHALL NOT require a TanStack Start SSR server process to render any application route. The browser entry SHALL initialize the existing file-based route tree and render the application into the static entry document.

#### Scenario: Static build is inspected
- **WHEN** a production frontend build completes
- **THEN** the output contains the static entry document and its referenced browser assets, and does not contain a required frontend server entry for application rendering

### Requirement: Existing routes remain browser-navigable
The static application SHALL preserve the existing route URLs, in-app link navigation, URL search state, scroll restoration, lazy browser-only browse table behavior, and direct loading of known public and protected routes. A static host SHALL return the application entry document for non-file client routes so that the client router can resolve them.

#### Scenario: Known deep link is opened directly
- **WHEN** a browser directly requests `/browse?type=url&pageSize=10` or `/admin/providers`
- **THEN** it receives the application entry document and the client router resolves the requested route after the JavaScript application starts

### Requirement: Same-origin API and session behavior are retained
The static application SHALL continue to call the existing same-origin `/api/*` surface, including request method, query string, request body, bearer authorization, API-error handling, and protected-route redirect behavior. The static host SHALL forward `/api/*` to the API without requiring a browser-visible cross-origin API URL.

#### Scenario: Browser session is restored before protected navigation
- **WHEN** a user with a valid stored Admin session opens `/admin/providers` directly
- **THEN** the browser restores the session, the client-side Admin guard permits the route, and protected API requests use the stored bearer token

#### Scenario: API login remains compatible
- **WHEN** the browser posts valid credentials to `/api/auth/login`
- **THEN** the API response is handled by the existing login flow and the user is routed according to the existing deep-link and role rules

### Requirement: Static hosting distinguishes client routes from missing assets
The static-host contract SHALL return the application entry document for client routes and SHALL return 404 for nonexistent asset-like paths. Existing frontend security and caching policy remains the responsibility of the E14 frontend production container change.

#### Scenario: Missing JavaScript asset is requested
- **WHEN** a browser requests a nonexistent JavaScript, CSS, image, or other file-like asset path
- **THEN** the static host returns 404 rather than the application entry document
