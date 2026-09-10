# phishtank-client Specification

## Purpose

Provides the PhishTank HTTP client that downloads the full `online-valid.json.gz` feed with a descriptive user agent, streaming, safe timeouts, optional app-key support, and deterministic quota signaling, without ever logging secrets.

## Requirements

### Requirement: PhishTank client identifies the application
The system SHALL send a descriptive user agent identifying the application when requesting the PhishTank feed.

#### Scenario: Request carries the application user agent
- **WHEN** the client requests the feed
- **THEN** the request includes a descriptive user agent header identifying the application

### Requirement: Streaming full-feed download
The system SHALL download `online-valid.json.gz` as a stream and decompress it without buffering the whole file in memory.

#### Scenario: Feed is streamed and decompressed
- **WHEN** the client downloads the feed
- **THEN** the response body is streamed and decompressed incrementally without materializing the entire archive in memory

### Requirement: Safe timeouts and cancellation
The system SHALL apply explicit timeouts and honor cancellation for PhishTank requests.

#### Scenario: Request exceeds timeout
- **WHEN** a PhishTank request exceeds the configured timeout
- **THEN** the client fails the request as a transport error rather than hanging

#### Scenario: Cancellation propagates
- **WHEN** a fetch is cancelled while the download is in progress
- **THEN** the request is cancelled cleanly and the failure is distinguishable from other transport errors

### Requirement: Optional app key is never logged
If an app key is configured for PhishTank, the system SHALL send it in the request without ever logging it, and SHALL NOT log any part of the URL or request that could expose it.

#### Scenario: Key never appears in logs
- **WHEN** a request is made with a configured app key
- **THEN** no log line, exception message, or diagnostic includes the key or the portion of the URL containing it

#### Scenario: No key configured still works
- **WHEN** no app key is configured
- **THEN** the client still requests the public feed without requiring authentication

### Requirement: Quota responses are signaled deterministically
The system SHALL surface a PhishTank HTTP 429/rate-limit response as a distinct quota condition so the worker can fail immediately without retry.

#### Scenario: 429 raised as quota condition
- **WHEN** the upstream responds with HTTP 429
- **THEN** the client raises a typed quota condition that is classified as non-retryable

#### Scenario: Other non-success responses are transport failures
- **WHEN** the upstream responds with a 5xx or another transport-level failure
- **THEN** the client fails the request as a transport error, which is retryable at the job level

#### Scenario: Authentication failure is terminal
- **WHEN** the upstream responds with HTTP 401 or 403 for a configured app key
- **THEN** the client reports a terminal authentication failure that is not retried