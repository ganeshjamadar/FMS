# FundManager — Quickstart Guide

> Developer setup for running the full FundManager platform locally.

## Prerequisites

| Tool          | Version    | Install                                    |
|---------------|------------|--------------------------------------------|
| .NET SDK      | 8.0 LTS    | https://dotnet.microsoft.com/download      |
| Node.js       | 20 LTS     | https://nodejs.org/                        |
| pnpm          | 9.x        | `npm i -g pnpm`                            |
| Docker        | 24+        | https://docs.docker.com/get-docker/        |
| Docker Compose| v2         | Bundled with Docker Desktop                |
| PostgreSQL    | 16         | Via Docker (see below)                     |
| Git           | 2.x        | https://git-scm.com/                       |

## Repository Layout

```
FundManager/
├── src/
│   ├── services/
│   │   ├── Identity/          # Identity.Api, Identity.Domain, Identity.Infrastructure
│   │   ├── FundAdmin/         # FundAdmin.Api, FundAdmin.Domain, FundAdmin.Infrastructure
│   │   ├── Contributions/     # Contributions.Api, Contributions.Domain, Contributions.Infrastructure
│   │   ├── Loans/             # Loans.Api, Loans.Domain, Loans.Infrastructure
│   │   ├── Dissolution/       # Dissolution.Api, Dissolution.Domain, Dissolution.Infrastructure
│   │   ├── Notifications/     # Notifications.Api, Notifications.Domain, Notifications.Infrastructure
│   │   └── Audit/             # Audit.Api, Audit.Domain, Audit.Infrastructure
│   ├── gateway/               # YARP API Gateway
│   ├── shared/
│   │   ├── Contracts/         # Shared event/message contracts (NuGet)
│   │   ├── BuildingBlocks/    # Cross-cutting (auth middleware, logging, EF base)
│   │   └── ServiceDefaults/   # .NET Aspire service defaults
│   ├── web/                   # React (Vite) web app
│   └── mobile/                # React Native (Expo) mobile app
├── tests/
│   ├── unit/                  # xUnit unit tests per service
│   ├── integration/           # Testcontainers-based integration tests
│   └── contract/              # Pact contract tests
├── infra/
│   ├── docker-compose.yml     # Full local stack
│   ├── docker-compose.dev.yml # Dev overrides (hot reload, debug ports)
│   └── init-db/               # SQL scripts for schema creation
├── specs/                     # Specifications, plans, contracts
├── FundManager.sln            # Solution file
└── README.md
```

## 1. Clone & Configure

```bash
git clone <repo-url> FundManager
cd FundManager
```

### Environment Variables

Copy the sample env file and adjust as needed:

```bash
cp .env.sample .env
```

Key variables:

```dotenv
# Database
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_USER=fundmanager
POSTGRES_PASSWORD=dev_password_123
POSTGRES_DB=fundmanager

# RabbitMQ
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest

# Redis
REDIS_HOST=localhost
REDIS_PORT=6379

# JWT
JWT_SECRET=<generate-256-bit-key>
JWT_EXPIRY_HOURS=24
```

## 2. Start Infrastructure (Docker Compose)

```bash
docker compose -f infra/docker-compose.yml up -d
```

This starts:
- **PostgreSQL 16** on port 5432
- **RabbitMQ 3.13** on port 5672 (management UI: http://localhost:15672)
- **Redis 7** on port 6379

Verify all containers are healthy:

```bash
docker compose -f infra/docker-compose.yml ps
```

## 3. Database Setup

### Create Schemas

The init-db scripts run automatically on first Docker Compose start. To run manually:

```bash
docker exec -i fundmanager-postgres psql -U fundmanager -d fundmanager < infra/init-db/01-schemas.sql
```

Expected schemas: `identity`, `fundadmin`, `contributions`, `loans`, `dissolution`, `notifications`, `audit`.

### Run EF Core Migrations

Each service has its own migrations. Run from the repo root:

```bash
# Identity service
dotnet ef database update --project src/services/Identity/Identity.Infrastructure --startup-project src/services/Identity/Identity.Api

# FundAdmin service
dotnet ef database update --project src/services/FundAdmin/FundAdmin.Infrastructure --startup-project src/services/FundAdmin/FundAdmin.Api

# Contributions service
dotnet ef database update --project src/services/Contributions/Contributions.Infrastructure --startup-project src/services/Contributions/Contributions.Api

# Loans service
dotnet ef database update --project src/services/Loans/Loans.Infrastructure --startup-project src/services/Loans/Loans.Api

# Dissolution service
dotnet ef database update --project src/services/Dissolution/Dissolution.Infrastructure --startup-project src/services/Dissolution/Dissolution.Api

# Notifications service
dotnet ef database update --project src/services/Notifications/Notifications.Infrastructure --startup-project src/services/Notifications/Notifications.Api

# Audit service
dotnet ef database update --project src/services/Audit/Audit.Infrastructure --startup-project src/services/Audit/Audit.Api
```

Or use the helper script:

```bash
./scripts/migrate-all.sh
```

## 4. Run Backend Services

### Option A: Run All via .NET Aspire (Recommended)

```bash
dotnet run --project src/AppHost/AppHost.csproj
```

Aspire dashboard: http://localhost:15197

### Option B: Run Individually

```bash
# Terminal 1 — API Gateway
dotnet run --project src/gateway/Gateway.Api

# Terminal 2–8 — Individual services
dotnet run --project src/services/Identity/Identity.Api
dotnet run --project src/services/FundAdmin/FundAdmin.Api
dotnet run --project src/services/Contributions/Contributions.Api
dotnet run --project src/services/Loans/Loans.Api
dotnet run --project src/services/Dissolution/Dissolution.Api
dotnet run --project src/services/Notifications/Notifications.Api
dotnet run --project src/services/Audit/Audit.Api
```

### Service Ports (Default)

| Service          | Port  |
|------------------|-------|
| API Gateway      | 5000  |
| Identity         | 5001  |
| FundAdmin        | 5002  |
| Contributions    | 5003  |
| Loans            | 5004  |
| Dissolution      | 5005  |
| Notifications    | 5006  |
| Audit            | 5007  |

All API calls go through the Gateway at `http://localhost:5000`.

## 5. Run Web App

```bash
cd src/web
pnpm install
pnpm dev
```

Web app runs at: http://localhost:5173

### Frontend Monorepo Structure

```
src/web/
├── packages/
│   └── shared/            # @fundmanager/shared (types, utils, hooks)
├── apps/
│   └── web/               # Main React app
├── pnpm-workspace.yaml
└── turbo.json
```

## 6. Run Mobile App

```bash
cd src/mobile
pnpm install
npx expo start
```

Scan the QR code with Expo Go (iOS/Android) or press `a` for Android emulator / `i` for iOS simulator.

## 7. Run Tests

### Unit Tests

```bash
dotnet test tests/unit/
```

### Integration Tests (requires Docker)

```bash
dotnet test tests/integration/
```

Integration tests use **Testcontainers** — they spin up PostgreSQL and RabbitMQ automatically.

### Frontend Tests

```bash
cd src/web
pnpm test
```

### Contract Tests (Pact)

```bash
dotnet test tests/contract/
```

## 8. Common Development Tasks

### Add a New EF Core Migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/services/<Service>/<Service>.Infrastructure \
  --startup-project src/services/<Service>/<Service>.Api \
  --output-dir Persistence/Migrations
```

Naming convention: `YYYYMMDD_HHMMSS_Service_Description`

### Seed Data

```bash
dotnet run --project src/services/Identity/Identity.Api -- --seed
```

Creates a Platform Admin user with phone `+919999999999`.

### API Documentation

Each service exposes Swagger UI when running in Development:

- Gateway: http://localhost:5000/swagger
- Identity: http://localhost:5001/swagger
- etc.

### Health Checks

All services expose `/health` and `/health/ready` endpoints.

```bash
curl http://localhost:5000/health
```

## 9. Docker Compose — Full Stack

To run everything (infra + all services + web) in Docker:

```bash
docker compose -f infra/docker-compose.yml -f infra/docker-compose.dev.yml up --build
```

## Troubleshooting

| Issue                              | Solution                                                     |
|------------------------------------|--------------------------------------------------------------|
| Port already in use                | `lsof -i :<port>` or change port in `launchSettings.json`   |
| EF migration fails                | Ensure PostgreSQL is running and connection string is correct |
| RabbitMQ connection refused        | Check container health: `docker logs fundmanager-rabbitmq`   |
| Mobile app can't reach API         | Use LAN IP instead of localhost in Expo config               |
| pnpm install fails                 | Clear cache: `pnpm store prune && pnpm install`             |
| Testcontainers timeout             | Ensure Docker daemon is running and has enough resources     |
