# Software Capabilities And API Usage

The platform provides two ways to use the system: the web application at `http://localhost:3000` and the ASP.NET Core API at `http://localhost:5092`. The frontend consumes the same API documented below. IOC discovery endpoints are anonymous for read-only investigation; authentication and all provider-management operations are protected.

## Capabilities

## Authentication

### Login

```http
POST /api/auth/login
Content-Type: application/json

{
	"username": "admin",
	"password": "your-password"
}
```

Successful login returns a JWT, username, role, and expiration time:

```json
{
  "token": "eyJ...",
  "username": "admin",
  "role": "Admin",
  "expiresAt": "2026-09-15T12:00:00+00:00"
}
```

Send the token to protected endpoints with:

```http
Authorization: Bearer <token>
```

Invalid credentials return `401 Unauthorized`. Requests to `/api/admin/*` also require the `Admin` role and return `403 Forbidden` for authenticated non-admin users.

## IOC Endpoints

These endpoints are read-only and anonymous. Results are grouped by normalized IOC identity and returned in a paged response.

| Method and endpoint            | Purpose                                                   |
| ------------------------------ | --------------------------------------------------------- |
| `GET /api/iocs`                | Browse IOC groups with filters, ordering, and pagination. |
| `GET /api/iocs/search`         | Search IOC groups by text, tag, and provider.             |
| `GET /api/iocs/{id}`           | Get source-attributed detail for one IOC row.             |
| `GET /api/iocs/{id}/relations` | Get related IOC groups for a host/domain relationship.    |

### Browse filters

`GET /api/iocs` accepts:

| Query parameter                | Meaning                                               |
| ------------------------------ | ----------------------------------------------------- |
| `type`                         | IOC type, such as `ipv4`, `ipv6`, `domain`, or `url`. |
| `provider`                     | Provider ID.                                          |
| `tag`                          | Normalized tag filter.                                |
| `firstSeenFrom`, `firstSeenTo` | Inclusive first-seen date range.                      |
| `lastSeenFrom`, `lastSeenTo`   | Inclusive last-seen date range.                       |
| `orderBy`                      | Supported ordering field, such as `iocValue`.         |
| `dir`                          | Sort direction, normally `asc` or `desc`.             |
| `page`, `pageSize`             | Page number and page size.                            |

Example:

```bash
curl 'http://localhost:5092/api/iocs?type=url&provider=1&tag=phishing&page=1&pageSize=20'
```

### Search and relations

Search accepts `q`, `tag`, `provider`, `page`, and `pageSize`:

```bash
curl 'http://localhost:5092/api/iocs/search?q=evil.example&tag=phishing'
```

Relations accepts `relation`, `page`, and `pageSize`. The supported relation values are `urlHost` and `parentDomain`:

```bash
curl 'http://localhost:5092/api/iocs/42/relations?relation=urlHost&page=1&pageSize=20'
```

## Admin Endpoints

Every endpoint in this section requires `Authorization: Bearer <admin-token>`.

### Provider management

| Method and endpoint                             | Purpose                                                                          |
| ----------------------------------------------- | -------------------------------------------------------------------------------- |
| `GET /api/admin/providers`                      | List provider configuration, supported modes, cooldowns, and latest fetch state. |
| `POST /api/admin/providers`                     | Create a provider.                                                               |
| `PUT /api/admin/providers/{providerId}`         | Update a provider.                                                               |
| `DELETE /api/admin/providers/{providerId}`      | Delete a provider if it has no IOC or fetch-history references.                  |
| `GET /api/admin/providers/{providerId}/history` | Get paginated fetch history for one provider.                                    |

Create and update use this payload shape:

```json
{
  "name": "Threat Provider",
  "baseUrl": "https://provider.example/api",
  "intervalMinutes": 60,
  "isEnabled": true,
  "defaultMode": "recent",
  "supportedModes": ["recent", "full"],
  "recentCooldownMinutes": 10,
  "fullCooldownMinutes": 60,
  "minimumIntervalMinutes": 15
}
```

Duplicate provider names return `400`. A provider referenced by IOC data or fetch history cannot be deleted and returns `409`.

### Fetch operations

| Method and endpoint                                 | Purpose                                                 |
| --------------------------------------------------- | ------------------------------------------------------- |
| `POST /api/admin/providers/{providerId}/fetch`      | Queue a manual fetch using the provider's default mode. |
| `POST /api/admin/providers/{providerId}/fetch/full` | Queue a manual full fetch.                              |
| `GET /api/admin/fetch-history/{jobId}`              | Get the status and result counters for one fetch job.   |

Fetch requests are asynchronous. A successful request returns `202 Accepted`:

```json
{
  "jobId": "8b2a9f4f-4fb5-4b1f-bd24-2d9c02d8c3f7",
  "status": "queued"
}
```

Use the returned `jobId` with the fetch-history endpoint. A missing provider returns `404`, an unsupported mode returns `400`, an active execution guard returns `409`, and a broker publishing failure returns `503`.

### Summary, logs, and settings

| Method and endpoint                           | Purpose                                                                                                                          |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/admin/summary`                      | Return distinct IOC totals for all, IPv4, IPv6, domains, and URLs.                                                               |
| `GET /api/admin/logs?count=100&level=Warning` | Return recent structured logs. `count` is 1-500; level can be `Verbose`, `Debug`, `Information`, `Warning`, `Error`, or `Fatal`. |
| `GET /api/admin/settings`                     | Return non-secret runtime log-level and provider settings.                                                                       |
| `PUT /api/admin/settings`                     | Atomically update the complete runtime settings snapshot.                                                                        |

Settings updates use:

```json
{
  "logLevel": "Information",
  "providers": [{ "id": 1, "isEnabled": true, "intervalMinutes": 60 }]
}
```

The interval cannot be lower than the provider's configured minimum. Secrets are not returned by the settings endpoint.

## Health And API Documentation

| Endpoint               | Purpose                                                                              |
| ---------------------- | ------------------------------------------------------------------------------------ |
| `GET /health`          | Dependency-aware health check for the API, SQL Server, and RabbitMQ.                 |
| `GET /health/live`     | Liveness check that confirms the process is running without evaluating dependencies. |
| `GET /openapi/v1.json` | Generated OpenAPI document. Development and Testing only.                            |
| `GET /scalar/v1`       | Interactive Scalar API reference. Development and Testing only.                      |

Open Scalar at [http://localhost:5092/scalar/v1](http://localhost:5092/scalar/v1) while the API runs in Development or Testing. Use the Bearer authentication control with the token returned by `/api/auth/login` to call protected operations.

## Processing Flow

1. An administrator triggers a provider fetch from the frontend or an admin API endpoint.
2. The API validates the provider and fetch mode, reserves a job, and publishes a `FetchJob` to RabbitMQ.
3. The Worker consumes one job at a time, applies provider-specific ingestion and normalization, and writes IOC, tag, relation, and fetch-history changes to SQL Server.
4. The frontend polls fetch history and displays the resulting status, counters, and errors.

The database is authoritative for fetch history. RabbitMQ provides delivery between API and Worker, while the fetch guard and job identity protect against overlapping and duplicate execution.

## Common Error Responses

Errors use the API problem format and include a machine-readable code, title, detail, status, and correlation information. Common statuses are:

- `400` for invalid query, provider, settings, or fetch mode input.
- `401` for missing or invalid authentication.
- `403` for insufficient role permissions.
- `404` for missing IOC, provider, or fetch job.
- `409` for guarded fetches or providers still referenced by data.
- `503` when a fetch job cannot be published to RabbitMQ.
