## Purpose

Delivers the compiled frontend as a reproducible, secret-free nginx image with correct API proxying, SPA routing, caching, compression, and browser protections.

## ADDED Requirements

### Requirement: Reproducible secret-free frontend image
The production image SHALL install dependencies from the committed lockfile, compile the frontend in a build stage, and serve only generated public artifacts from a minimal non-root runtime. The build context, image layers, runtime files, and environment SHALL NOT contain `.env`, JWT secrets, provider keys, database or broker credentials, or admin credentials.

#### Scenario: Image is inspected for secrets
- **WHEN** the final image and runtime environment are scanned with known canary values
- **THEN** no configuration secret or repository `.env` content is present

### Requirement: API proxy and SPA navigation
The frontend server SHALL proxy `/api/*` to the internal API while preserving method, body, query string, and correlation headers. Non-file client routes SHALL return the SPA entry document; missing asset paths SHALL return 404 rather than the SPA document.

#### Scenario: Protected route is opened directly
- **WHEN** a browser requests `/admin/providers` from a fresh navigation
- **THEN** the frontend returns the application shell and client routing processes the protected route

#### Scenario: API login is proxied
- **WHEN** the browser posts to `/api/auth/login`
- **THEN** the request reaches the API unchanged and its status, body, and correlation header return to the browser

#### Scenario: Asset is missing
- **WHEN** a request targets a nonexistent file-like asset path
- **THEN** the server returns 404 and does not return the SPA entry document

### Requirement: Production response policy
Hashed immutable assets SHALL receive long-lived immutable caching; the SPA entry document SHALL be revalidated. Compressible responses SHALL use compression when accepted. Responses SHALL include a CSP compatible with the built application, content-type sniffing protection, frame protection, a restrictive referrer policy, and a restrictive permissions policy. HSTS SHALL NOT be required inside the HTTP-only Compose network where TLS termination is external.

#### Scenario: Entry and asset headers differ
- **WHEN** an operator inspects the SPA entry and a hashed asset
- **THEN** both carry security headers while only the hashed asset receives immutable caching

### Requirement: Frontend liveness
The frontend container SHALL expose an unauthenticated `/healthz` endpoint that verifies nginx/static serving without depending on API authentication or readiness.

#### Scenario: Frontend is serving files
- **WHEN** Compose probes `/healthz`
- **THEN** the endpoint succeeds without a session or API call
