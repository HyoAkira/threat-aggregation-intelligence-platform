# user-frontend-shell-auth Specification

## Purpose

Provides an accessible responsive application shell and a safe browser session that keeps IOC browsing open without login and enforces login and role-aware navigation for protected workflows.

## Requirements

### Requirement: Responsive accessible shell
The frontend SHALL provide consistent navigation, page headings, keyboard-visible focus, labelled controls, and explicit loading, empty, and error states at mobile and desktop widths. User navigation SHALL include Search and Browse; Admin navigation SHALL additionally expose Admin destinations.

#### Scenario: Keyboard navigation
- **WHEN** a keyboard-only user moves through the application shell
- **THEN** every interactive control is reachable in logical order and has a visible focus state and accessible name

### Requirement: Login and session lifecycle
`/login` SHALL accept username and password, call `POST /api/auth/login`, store the returned session only in browser session storage, and route the user to the originally requested protected page or `/search`. Logout, token expiry, or any API 401 on a protected endpoint SHALL clear the token and cached protected data and return the user to login with safe feedback.

#### Scenario: Deep link resumes after login
- **WHEN** an unauthenticated user opens a protected page and then logs in successfully
- **THEN** the frontend returns the user to that page

#### Scenario: Expired session is cleared
- **WHEN** the token expires or a protected API request returns 401
- **THEN** protected cached data is removed and the user is redirected to login

### Requirement: Client-side guards complement server authorization
Public IOC routes (`/search`, `/iocs/:id`, `/browse`) SHALL render and query the anonymous IOC endpoints without a session. Protected routes SHALL require a current session, and Admin routes SHALL additionally require the Admin claim, while the API remains authoritative. A 403 SHALL show a forbidden state and SHALL NOT retry indefinitely.

#### Scenario: Anonymous IOC browsing
- **WHEN** an unauthenticated user opens `/search`, `/browse`, or an IOC detail page
- **THEN** the page renders and loads IOC data without requiring login

#### Scenario: User cannot open Admin route
- **WHEN** a signed-in User navigates to an Admin-only route
- **THEN** the frontend shows a forbidden state or redirects to Search without rendering Admin data
