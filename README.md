# Threat Intelligence Aggregator

## Prerequisites

- .NET SDK 10
- Node.js 22+
- Docker Compose v2

## First-time setup

1. Copy `.env.example` to `.env` and replace every `CHANGE_ME` value. Do not commit `.env`.
2. Restore and build the backend: `dotnet restore ThreatIntelligence.slnx` then `dotnet build ThreatIntelligence.slnx`.
3. Install the frontend packages: `npm ci --prefix frontend`.
4. Start the local platform: `docker compose up --build`.

The API readiness endpoint is `http://localhost:8080/health`; the Worker readiness endpoint is
`http://localhost:8081/health`. RabbitMQ management is available on `http://localhost:15672`.

## Everyday commands

```bash
dotnet test ThreatIntelligence.slnx
npm test --prefix frontend
npm run lint --prefix frontend
npm run build --prefix frontend
docker compose up --build
docker compose --profile test up testsqlserver
docker compose down
```

`docker compose down` preserves database, broker, and log volumes. Use `docker compose down -v` only
when intentionally resetting local data.

## Conventions

- C# uses PascalCase public members; EF Core maps them to the lower-camel-case schema in E02.
- Application owns CQRS handlers and interfaces; Infrastructure owns implementations and external IO.
- Keep secrets in `.env`; use `.env.example` for documentation only.
