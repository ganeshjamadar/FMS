# Implementation Plan: Generic Multi-Fund Monthly Contribution & Reducing-Balance Lending System

**Branch**: `001-fund-lending-system` | **Date**: 2026-02-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-fund-lending-system/spec.md`

## Summary

A community fund management platform enabling pooled monthly contributions, reducing-balance lending, repayment tracking, and fair dissolution settlement. The system uses a microservices architecture with .NET 8 APIs, React web client (admin-heavy), React Native mobile client (member-focused), and PostgreSQL databases. Each fund acts as a logical tenant boundary. Key capabilities: RBAC with 4 roles, OTP authentication, monthly contribution cycles, loan approval with optional voting, reducing-balance interest calculations, dissolution with proportional interest sharing, and multi-channel notifications.

## Technical Context

**Language/Version**: C# / .NET 8 (LTS) for API services; TypeScript 5.x for React & React Native
**Primary Dependencies**:
- **API**: ASP.NET Core 8, Entity Framework Core 8, MassTransit (async messaging), FluentValidation, Serilog, OpenTelemetry
- **Web**: React 18, React Router, TanStack Query, Zustand (state), Tailwind CSS, Vite
- **Mobile**: React Native 0.73+, Expo (managed workflow), React Navigation, TanStack Query, AsyncStorage (offline queue)
- **Infrastructure**: RabbitMQ (message broker), Redis (caching/distributed locks), Seq or ELK (logging)
**Storage**: PostgreSQL 16 (one logical database per service, schema-per-service isolation)
**Testing**: xUnit + FluentAssertions + Testcontainers (.NET); Jest + React Testing Library (Web); Jest + React Native Testing Library (Mobile); Pact (contract tests between services)
**Target Platform**: Linux containers (Docker) for services; Web (modern browsers); iOS 15+ / Android 10+ for mobile
**Project Type**: Microservices + Web + Mobile
**Performance Goals**: API p95 < 200ms; contribution cycle for 1,000 members < 60s; report generation < 30s; notifications delivered within 60s
**Constraints**: 99.5% availability (single-region MVP); offline-capable mobile (queue writes); all monetary values as `decimal` (no floating-point); UTC storage / IST display
**Scale/Scope**: 100+ concurrent funds, 1,000 members per fund, ~50 screens (web + mobile combined), 7 microservices

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 Check (PASSED)

| # | Constitution Principle | Status | Evidence |
|---|------------------------|--------|----------|
| I | Multi-Fund Segregation Is Mandatory | **PASS** | Every service scopes queries by `FundId`; schema-per-service isolation in PostgreSQL; API endpoints require fund context; cross-fund data mixing is blocked at the API gateway level |
| II | Financial Calculations Must Be Deterministic and Explainable | **PASS** | All monetary values stored as `decimal(18,2)` in PostgreSQL; C# `decimal` type used throughout; reducing-balance formula inputs stored on each RepaymentEntry; banker's rounding (MidpointRounding.ToEven) applied consistently |
| III | Complete Auditability and Traceability | **PASS** | Dedicated Audit service with append-only AuditLog table; every state-changing action emits an audit event via message bus; corrections via reversal entries only; all financial events carry actor, timestamp, source, reason |
| IV | Fairness, Transparency, and Dissolution Correctness | **PASS** | Dissolution settlement formula documented in spec (Section 7.3); calculation inputs stored on DissolutionLineItem; reports show calculation basis per member; interest sharing formula is fund-configurable |
| V | Security and Privacy by Default | **PASS** | RBAC with 4 roles enforced server-side on every endpoint; members access only their data + fund aggregates; TLS 1.2+ in transit; encryption at rest via PostgreSQL TDE/volume encryption; structured logging excludes secrets/PII |
| OC-1 | Localhost-first development | **PASS** | Docker Compose for local dev; all services default to localhost/loopback |
| OC-2 | Offline mobile with server as source of truth | **PASS** | React Native queues writes in AsyncStorage when offline; sync on reconnect with conflict resolution preserving ledger integrity |
| OC-3 | UTC storage / local display | **PASS** | All timestamps stored as `timestamptz` (UTC); clients convert to IST for display |
| OC-4 | Repayment allocation order | **PASS** | FR-068 confirms: interest first, then principal, then excess to principal reduction |
| OC-5 | Tests for all financial logic | **PASS** | xUnit tests for contribution, disbursement, reducing-balance interest, repayment allocation, dissolution; Testcontainers for integration; Pact for contract tests |
| OC-6 | Schema/API changes require migration notes | **PASS** | EF Core migrations with explicit migration files; OpenAPI contracts versioned in `/contracts/` |

### Post-Phase 1 Re-evaluation (PASSED)

| # | Constitution Principle | Status | Phase 1 Evidence |
|---|------------------------|--------|------------------|
| I | Multi-Fund Segregation | **PASS** | `data-model.md`: All 20 entities carry `FundId` column; 7 PostgreSQL schemas (`identity`, `fundadmin`, `contributions`, `loans`, `dissolution`, `notifications`, `audit`); OpenAPI contracts scope all fund-specific endpoints under `/api/funds/{fundId}/`; cross-service references are logical (no cross-schema FKs) |
| II | Deterministic Financials | **PASS** | `data-model.md`: All money fields use `numeric(18,2)` / C# `decimal`; rates use `numeric(8,6)`; Loan entity captures snapshot fields (`MonthlyInterestRate`, `MinimumPrincipal`, `ScheduledInstallment`) at disbursement for reproducibility; `RepaymentEntry` stores `InterestDue`, `PrincipalDue` separately; `contracts/loans-api.yaml`: `recordRepayment` response decomposes `interestPaid`, `principalPaid`, `excessAppliedToPrincipal` |
| III | Auditability | **PASS** | `data-model.md`: `AuditLog` entity is append-only with immutability trigger; stores `BeforeState`/`AfterState` as JSONB; `Transaction` entity is append-only; `contracts/audit-api.yaml`: query endpoints with date-range partition pruning, entity-history timeline; all financial mutations require `Idempotency-Key` header |
| IV | Dissolution Correctness | **PASS** | `data-model.md`: `DissolutionSettlement` + `DissolutionLineItem` entities store full per-member breakdown (`TotalPaidContributions`, `InterestShare`, `OutstandingLoanPrincipal`, `UnpaidInterest`, `GrossPayout`, `NetPayout`); `contracts/dissolution-api.yaml`: `/confirm` blocked if any `netPayout < 0`; `/report` exports PDF/CSV showing calculation basis |
| V | Security & Privacy | **PASS** | `contracts/identity-api.yaml`: OTP-based auth with rate limiting (5/15min); all endpoints require `BearerAuth`; role-based access (Admin/Editor/Guest) enforced per endpoint; `data-model.md`: OTP stored with bcrypt hash; PII not logged; session expiry after 24h inactivity |
| OC-1 | Localhost-first | **PASS** | `quickstart.md`: Docker Compose setup documented; all services default to `localhost`; .NET Aspire option for orchestration |
| OC-2 | Offline mobile | **PASS** | `research-frontend.md`: AsyncStorage offline queue with `OfflineSyncManager`; server remains source of truth; conflict resolution via optimistic concurrency (If-Match headers in API contracts) |
| OC-3 | UTC / local display | **PASS** | `data-model.md`: All date columns use `timestamptz`; `research-database.md`: confirmed UTC storage; clients convert to IST |
| OC-4 | Repayment order | **PASS** | `contracts/loans-api.yaml`: `recordRepayment` response explicitly decomposes `interestPaid` → `principalPaid` → `excessAppliedToPrincipal`; `data-model.md`: RepaymentEntry stores `InterestDue` and `PrincipalDue` separately |
| OC-5 | Financial tests | **PASS** | `quickstart.md`: Testing commands documented (unit, integration, contract); `research.md`: xUnit + Testcontainers + Pact test strategy; `data-model.md`: validation rules reference specific FR numbers for test coverage |
| OC-6 | Migration notes | **PASS** | `research-database.md`: EF Core migration strategy with naming convention `YYYYMMDD_HHMMSS_Service_Description`; no destructive changes on financial tables; OpenAPI contracts versioned in `contracts/` |

**Gate Result**: **ALL PASS (12/12)** — Phase 1 design artifacts are constitutionally compliant.

## Project Structure

### Documentation (this feature)

```text
specs/001-fund-lending-system/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (OpenAPI specs per service)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── services/
│   ├── FundManager.Identity/          # Auth & Profile microservice (.NET 8)
│   │   ├── Api/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   └── Tests/
│   ├── FundManager.FundAdmin/         # Fund & Membership microservice (.NET 8)
│   │   ├── Api/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   └── Tests/
│   ├── FundManager.Contributions/     # Contributions & Ledger microservice (.NET 8)
│   │   ├── Api/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   └── Tests/
│   ├── FundManager.Loans/             # Loans, Repayments & Voting microservice (.NET 8)
│   │   ├── Api/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   └── Tests/
│   ├── FundManager.Dissolution/       # Dissolution & Settlement microservice (.NET 8)
│   │   ├── Api/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   └── Tests/
│   ├── FundManager.Notifications/     # Notification dispatch microservice (.NET 8)
│   │   ├── Api/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   └── Tests/
│   └── FundManager.Audit/            # Audit trail microservice (.NET 8)
│       ├── Api/
│       ├── Domain/
│       ├── Infrastructure/
│       └── Tests/
├── gateway/
│   └── FundManager.ApiGateway/        # API Gateway (YARP / .NET 8)
├── shared/
│   ├── FundManager.Contracts/         # Shared DTOs, events, interfaces
│   ├── FundManager.BuildingBlocks/    # Cross-cutting: auth, middleware, decimal helpers
│   └── FundManager.ServiceDefaults/   # OpenTelemetry, health checks, logging defaults
├── web/
│   └── fund-manager-web/             # React 18 + Vite + TypeScript
│       ├── src/
│       │   ├── components/
│       │   ├── pages/
│       │   ├── hooks/
│       │   ├── services/            # API client layer
│       │   ├── stores/
│       │   └── utils/
│       └── tests/
├── mobile/
│   └── fund-manager-mobile/          # React Native + Expo + TypeScript
│       ├── src/
│       │   ├── components/
│       │   ├── screens/
│       │   ├── navigation/
│       │   ├── hooks/
│       │   ├── services/            # API client + offline queue
│       │   ├── stores/
│       │   └── utils/
│       └── tests/
└── infrastructure/
    ├── docker-compose.yml            # Local dev: all services + PostgreSQL + RabbitMQ + Redis
    ├── docker-compose.override.yml   # Dev overrides
    └── scripts/                      # DB seed, migration runners
```

**Structure Decision**: Microservices architecture with 7 domain services + 1 API gateway. Each service follows Clean Architecture (Api → Domain → Infrastructure). Shared NuGet packages in `shared/`. Web and mobile are separate TypeScript projects sharing API client types generated from OpenAPI contracts.

### Phase 18 Additions (Notification Channel Providers)

```text
src/shared/FundManager.BuildingBlocks/Notifications/
├── ISmsSender.cs                  # SMS channel abstraction
├── IEmailSender.cs                # Email channel abstraction
├── IPushNotificationSender.cs     # Push channel abstraction
├── IRecipientResolver.cs          # Recipient contact resolution + ChannelContact record
└── IFundMemberResolver.cs         # Fund-wide broadcast member resolution

src/services/FundManager.Notifications/Infrastructure/Providers/
├── ConsoleSmsSender.cs            # Dev: logs SMS to console
├── SmtpEmailSender.cs             # Dev: sends via MailHog SMTP
├── ConsolePushSender.cs           # Dev: logs push to console
├── HttpRecipientResolver.cs       # Resolves user contact info from Identity service
└── HttpFundMemberResolver.cs      # Resolves fund member IDs from FundAdmin service

src/services/FundManager.Notifications/Infrastructure/Consumers/
└── OtpRequestedConsumer.cs        # Handles OTP delivery via SMS/email

src/services/FundManager.Identity/Api/Controllers/
└── ProfileController.cs           # Internal user profile endpoint for cross-service resolution

infrastructure/docker-compose.yml
└── mailhog service (port 8025)    # Dev email testing UI
```

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| 7 microservices (exceeds typical 3-project limit) | Domain boundaries are naturally distinct (identity, fund admin, contributions, loans, dissolution, notifications, audit); each has its own data ownership and scaling profile | A monolith would tightly couple financial ledger operations with notification dispatch and audit logging; schema-per-service isolation enforces Constitution Principle I (fund segregation) at the database level |
| API Gateway (8th deployable) | Single entry point for clients; handles auth token validation, rate limiting, request routing | Without a gateway, each client must know all service URLs and handle cross-cutting concerns independently |
| Shared NuGet packages (3 projects) | Shared contracts prevent DTO drift between services; building blocks enforce consistent decimal handling and audit middleware | Copy-pasting shared code across 7 services would create consistency violations against Constitution Principles II and III |
