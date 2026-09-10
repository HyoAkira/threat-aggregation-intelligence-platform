## Purpose

Provides the authenticated ThreatFox HTTP client that streams recent and full export data without logging the API key, honoring timeouts and cancellation, with retry ownership delegated to the messaging layer.

## ADDED Requirements

### Requirement: Export URL construction by mode
The client SHALL build the recent export URL from the configured ThreatFox base URL and API key when requesting `Recent`, and the full `.json.zip` export URL when requesting `Full`. The mode-to-URL mapping SHALL be deterministic and the selected URL SHALL be observable for verification.

#### Scenario: Recent export URL
- **WHEN** the client is asked to fetch the ThreatFox recent export
- **THEN** it requests the configured recent-export endpoint containing the API key

#### Scenario: Full export URL
- **WHEN** the client is asked to fetch the ThreatFox full export
- **THEN** it requests the configured full `.json.zip` endpoint containing the API key

#### Scenario: Base URL reused
- **WHEN** the client constructs either export URL
- **THEN** it derives the endpoint from the configured ThreatFox base URL

### Requirement: Secret redaction
The client SHALL never write the API key, the full export URL, or any header containing the key to logs, exception messages, or structured diagnostics. Requests SHALL carry a descriptive user agent identifying the application.

#### Scenario: Key not logged
- **WHEN** a request is made or fails
- **THEN** the API key and the credential-bearing URL are absent from all log and exception output

#### Scenario: User agent identifies the application
- **WHEN** a request is made
- **THEN** the HTTP request carries a user agent that names the application

### Requirement: Streaming, timeout, cancellation, and no retry
The client SHALL open the response as a stream rather than buffering the whole payload, apply an explicit request timeout, accept a cancellation token, and SHALL NOT retry failed requests itself.

#### Scenario: Response streamed
- **WHEN** a successful response is received
- **THEN** the client returns the response stream for the caller to consume incrementally

#### Scenario: Timeout enforced
- **WHEN** a request exceeds the configured timeout
- **THEN** the client fails with a timeout condition without retrying

#### Scenario: Cancellation honored
- **WHEN** the caller cancels the request
- **THEN** the client stops work promptly and does not continue the request

#### Scenario: No client-level retry
- **WHEN** a request fails transiently
- **THEN** the client returns the failure to the caller and does not retry independently

### Requirement: HTTP failures support the shared retry policy
The client SHALL distinguish network, timeout, and HTTP 5xx failures as retryable transport failures. HTTP 429/quota and HTTP 401/403 authentication failures SHALL be terminal. Cancellation requested by the caller SHALL remain cancellation rather than being converted into a retryable timeout.

#### Scenario: Server failure is retryable at job level
- **WHEN** ThreatFox returns HTTP 5xx
- **THEN** the client reports a retryable transport failure without retrying locally

#### Scenario: Quota or authentication failure is terminal
- **WHEN** ThreatFox returns HTTP 429, 401, or 403
- **THEN** the client reports a terminal typed failure that E05 does not retry
