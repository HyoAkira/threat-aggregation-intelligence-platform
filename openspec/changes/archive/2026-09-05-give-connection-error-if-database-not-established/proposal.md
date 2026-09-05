## Why

When the database connection is not active, the application starts up anyway and fails later with confusing, indirect errors. Fail-fast behavior at startup gives operators a clear, immediate signal that the database is unreachable before the application accepts traffic.

## What Changes

- **MODIFIED**: Add a startup health check that verifies the database connection is established. If the connection cannot be made, the application fails to start with a clear error message.
- The health check runs during application startup (before the host accepts requests) and attempts to open a connection to the configured database.
- If the connection fails, the application logs a clear error ("Database connection cannot be established") and does not boot.

## Capabilities

### New Capabilities
- `startup/database-health-check`: A new health check that verifies database connectivity at application startup and fails fast if the connection cannot be established.

### Modified Capabilities
(none)

## Impact

- Affects application startup sequence.
- No changes to existing database schema or stored procedure logic.
- Operators get immediate feedback if the database is unreachable or credentials are invalid.
