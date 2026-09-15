# Threat Aggregation Intelligence Platform

Threat Aggregation Intelligence Platform is a threat-intelligence ingestion and investigation system that collects indicators of compromise from external providers, normalizes and validates them, persists searchable IOC and relationship data, and exposes authenticated APIs and a web interface for browsing, searching, administration, and fetch operations. The API publishes fetch jobs through RabbitMQ, while a dedicated Worker processes provider feeds and writes durable results to SQL Server.

## Project Structure

```text
.
├── frontend/                         TanStack Start/React web application
├── src/
│   ├── ThreatIntelligence.API/        ASP.NET Core HTTP API and Scalar docs
│   ├── ThreatIntelligence.Application/Use cases, contracts, validation, normalization
│   ├── ThreatIntelligence.Domain/     Domain entities and business rules
│   ├── ThreatIntelligence.Infrastructure/
│   │                                    EF Core persistence, providers, messaging,
│   │                                    authentication, storage, and health checks
│   └── ThreatIntelligence.Worker/     Scheduled and RabbitMQ-backed fetch processing
├── tests/
│   ├── ThreatIntelligence.UnitTests/
│   └── ThreatIntelligence.IntegrationTests/
├── openspec/                          Requirements, design notes, and implementation tasks
├── ProjectDocs/                        Product and technical documentation
├── docker-compose.yml                  Local SQL Server and RabbitMQ services
└── ThreatIntelligence.slnx             .NET solution
```

## Architecture And Rationale Decisions

| Decision                                         | Why                                                                                                                                                                                    |
| ------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Layered .NET backend                             | Domain rules stay independent of infrastructure, application services coordinate use cases, and adapters can evolve without spreading persistence or provider details through the API. |
| Separate API and Worker hosts                    | HTTP requests remain responsive while scheduled and provider fetches run as bounded background work with independent retry and lifecycle handling.                                     |
| RabbitMQ with MassTransit                        | Fetch jobs need durable, confirmed delivery and controlled worker concurrency; the broker also decouples API triggers from provider latency and failures.                              |
| SQL Server with EF Core                          | IOC identity, relationships, fetch history, authentication, and administration require transactional persistence, constraints, and queryable relational data.                          |
| Normalization before persistence                 | Canonical URLs, domains, IPs, hashes, and provider records reduce duplicate indicators and make search and correlation predictable.                                                    |
| JWT authentication with role-based authorization | API access is authenticated by default, while administrative operations are restricted to the Admin role.                                                                              |
| OpenAPI plus Scalar                              | The API contract is generated from the ASP.NET Core endpoints and Scalar provides a practical interactive reference for local development and testing.                                 |

## Setup

### Prerequisites

- .NET SDK 10
- Node.js and pnpm
- Docker with Docker Compose

### Configure the environment

Copy the example environment file and replace every placeholder, especially database, RabbitMQ, provider, JWT, and administrator credentials:

```bash
cp .env.example .env
```

### Start dependencies

```bash
docker compose up -d mssql rabbitmq
```

The compose services expose SQL Server on `localhost:1433` and RabbitMQ on `localhost:5672`. RabbitMQ management is available on `localhost:15672`.

### Run the backend

In separate terminals:

```bash
dotnet run --project src/ThreatIntelligence.API
dotnet run --project src/ThreatIntelligence.Worker
```

The API initializes the database on startup. Its development URL is `http://localhost:5092` (HTTPS is also available through the launch profile).

### Run the frontend

```bash
cd frontend
pnpm install
pnpm dev
```

The frontend is served at `http://localhost:3000` and uses the local API target configured in `frontend/vite.config.ts`.

### Run tests and checks

```bash
dotnet test
cd frontend
pnpm test
pnpm lint
pnpm build
```

Integration tests that require an isolated SQL Server can use the Compose test profile:

```bash
docker compose --profile test up -d testsqlserver
```

## How To Use

1. Start SQL Server and RabbitMQ, then run the API and Worker.
2. Open `http://localhost:3000` and sign in with the administrator configured in `.env`.
3. Use the browse and search screens to inspect IOC groups, details, relations, and provider attribution.
4. Use the administration screens to review provider status, trigger default or full fetches, inspect fetch history and logs, and update runtime settings.
5. Monitor service health at `http://localhost:5092/health` and `http://localhost:5092/health/live`.

For detailed software capabilities, API endpoints, authentication, Docker usage,
health checks, processing flow, and common errors, see [USAGE.md](USAGE.md).

### Scalar API Reference

When the API runs in Development or Testing, open [Scalar](http://localhost:5092/scalar/v1) for an interactive API reference. The generated OpenAPI document is available at `http://localhost:5092/openapi/v1.json`. Scalar includes the documented Bearer authentication scheme, so authenticate with a JWT obtained from the login endpoint before calling protected operations.

Scalar is intentionally not mapped in other environments by this application. For deployed environments, expose the API contract and interactive documentation only according to the deployment's security policy.

## Docker Setup And Usage

Docker Compose runs the backend stack as containers: SQL Server, RabbitMQ, the
API, and the Worker. The frontend is not part of the current Compose file, so
run it separately with pnpm.

### Start the containerized backend

```bash
cp .env.example .env
# Replace the placeholders in .env before starting the stack.
docker compose up --build -d
```

The containerized services are available at:

| Service             | Host address             | Purpose                        |
| ------------------- | ------------------------ | ------------------------------ |
| API                 | `http://localhost:8080`  | HTTP API and health endpoints. |
| Worker              | `http://localhost:8081`  | Worker health endpoints.       |
| SQL Server          | `localhost:1433`         | Application database.          |
| RabbitMQ            | `localhost:5672`         | Fetch-job message broker.      |
| RabbitMQ management | `http://localhost:15672` | Broker administration UI.      |

Check startup and health with:

```bash
docker compose ps
docker compose logs -f api worker
curl http://localhost:8080/health
curl http://localhost:8080/health/live
```

Use the Docker API by replacing the local development port in examples with
`8080`, for example:

```bash
curl http://localhost:8080/api/iocs
```

Stop the services while retaining named volumes:

```bash
docker compose down
```

Only remove volumes when you intentionally want to delete local database,
RabbitMQ, and service data:

```bash
docker compose down -v
```

### Run the frontend against Docker

With the Compose backend running, start the frontend in another terminal:

```bash
cd frontend
pnpm install
pnpm dev
```

The frontend remains available at `http://localhost:3000`; its Vite proxy sends
API requests to the Docker API at `http://localhost:8080` only when configured
for that target. The default development proxy targets `http://localhost:5092`,
so update `frontend/vite.config.ts` or use the repository's Docker frontend
configuration when switching from the local .NET API to Docker.

### Scalar is unavailable in Docker

The API and Worker Dockerfiles publish `Release` builds, and the Compose
services do not set `ASPNETCORE_ENVIRONMENT` to Development or Testing. The API
maps `/openapi/v1.json` and `/scalar/v1` only in those two environments, so
neither the OpenAPI document nor Scalar is available from the Docker API at
`http://localhost:8080`.

To use Scalar, run the API locally in Development and open
[http://localhost:5092/scalar/v1](http://localhost:5092/scalar/v1). Use Docker
for the production-like backend workflow and the local API only when interactive
API documentation is needed.
