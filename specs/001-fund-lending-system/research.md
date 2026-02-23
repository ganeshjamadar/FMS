# Architecture Research: .NET 8 Microservices for Fund Management System

**Date**: 2026-02-20  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Scope**: Technology decisions for 7 microservices + API gateway serving a multi-fund community lending platform

---

## Table of Contents

1. [Service Decomposition & Bounded Contexts](#1-service-decomposition--bounded-contexts)
2. [Inter-Service Communication](#2-inter-service-communication)
3. [Data Isolation & Multi-Tenancy](#3-data-isolation--multi-tenancy)
4. [Saga / Process Manager Pattern](#4-saga--process-manager-pattern)
5. [API Gateway](#5-api-gateway)
6. [Financial Calculation Safety](#6-financial-calculation-safety)
7. [Idempotency](#7-idempotency)
8. [Concurrency Control](#8-concurrency-control)

---

## 1. Service Decomposition & Bounded Contexts

### Decision

Decompose into **7 domain services + 1 API gateway**, each aligned with a DDD bounded context:

| Service | Bounded Context | Owns | Key Aggregates |
|---------|----------------|------|----------------|
| **FundManager.Identity** | Identity & Access | Users, authentication, sessions, OTP | `User`, `Session`, `OtpChallenge` |
| **FundManager.FundAdmin** | Fund Administration | Fund lifecycle, membership, roles, contribution plans | `Fund`, `FundRoleAssignment`, `MemberContributionPlan` |
| **FundManager.Contributions** | Contributions & Ledger | Monthly dues, payments, fund ledger | `ContributionDue`, `Transaction` (contribution type) |
| **FundManager.Loans** | Lending & Voting | Loan lifecycle, repayments, voting | `Loan`, `RepaymentEntry`, `VotingSession`, `Vote` |
| **FundManager.Dissolution** | Settlement | Dissolution workflow, settlement calculations | `DissolutionSettlement`, `DissolutionLineItem` |
| **FundManager.Notifications** | Notification Dispatch | Notification templates, delivery, retry | `Notification`, `NotificationPreference` |
| **FundManager.Audit** | Audit & Compliance | Append-only audit log | `AuditLog` |

### Rationale

Each service boundary follows the **business capability** decomposition pattern, aligned with the spec's module structure (Sections 3–11). Key heuristics applied:

1. **Data ownership**: Each service is the single source of truth for its aggregates. No shared tables. Cross-service data is replicated via events (eventual consistency) or fetched via synchronous calls where strong consistency is required.

2. **Independent deployability**: Each service can be versioned, scaled, and deployed independently. Contributions and Loans are the highest-throughput services (monthly batch + member interactions) and can scale independently of Identity or Audit.

3. **Team autonomy**: Each bounded context maps to a potential team boundary. Notifications and Audit are infrastructure-level services with a stable interface consumed by all domain services.

4. **Failure isolation**: A failure in Notifications (e.g., SMS provider down) must not block payment recording in Contributions. Async boundaries enforce this.

### Boundary interaction rules

- **Identity → All services**: JWT token validation. Services validate tokens locally (asymmetric keys). No synchronous call to Identity on every request.
- **FundAdmin → Contributions/Loans**: When membership changes, FundAdmin publishes `MemberJoined`, `MemberRemoved` events. Contributions subscribes to create/stop dues. Loans subscribes to enforce eligibility.
- **Contributions → Loans**: Contributions publishes `ContributionRecorded`. Loans may subscribe for eligibility checks. The Dissolution service subscribes to aggregate totals.
- **Loans → Contributions**: Loan disbursement publishes `LoanDisbursed`. The Contributions ledger subscribes to record the disbursement transaction.
- **All services → Audit**: Every state-changing operation publishes an audit event. Audit consumes asynchronously.
- **All services → Notifications**: Domain events trigger notification dispatch via the message bus.

### Anti-corruption layers

Each service maintains its own read model of cross-service data it needs:

- **Contributions** stores a local cache of `MemberContributionPlan` (amount, fund, member) — sourced from FundAdmin events.
- **Loans** stores a local cache of member eligibility status — sourced from FundAdmin events.
- **Dissolution** queries Contributions and Loans via synchronous API calls during settlement calculation (strong consistency required for financial correctness).

### Alternatives considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Monolith first** | The domain has naturally distinct bounded contexts with different scaling profiles. A monolith would couple the high-throughput contribution cycle with low-frequency dissolution. The spec already identifies 7 modules. Starting as a monolith would require a costly decomposition later. |
| **Fewer services (3–4)** | Combining Contributions + Loans into one service was considered. Rejected because: (a) Contributions is a time-triggered batch process; Loans is user-initiated and event-driven. (b) Their data models are distinct. (c) They have different scaling characteristics. |
| **More services (e.g., separate Voting service)** | Voting is tightly coupled to Loan approval workflow. Extracting it would create chatty inter-service communication for a single workflow. Keeping it within Loans reduces latency and complexity. |

### Risks & Gotchas

- **Distributed data consistency**: Cross-service operations (e.g., dissolution reading from Contributions + Loans) require careful coordination. Mitigated by saga pattern (Section 4) and idempotent event handlers.
- **Service proliferation overhead**: 7 services + gateway = 8 deployables for a small team. Mitigated by shared build infrastructure (`FundManager.ServiceDefaults`, `FundManager.BuildingBlocks`) and Docker Compose for local dev.
- **Shared contract drift**: Mitigated by `FundManager.Contracts` NuGet package containing all published event schemas and DTOs. Breaking changes require version bumps with backward-compatible evolution.

---

## 2. Inter-Service Communication

### Decision

**Hybrid approach**: Asynchronous messaging as the **default**, synchronous HTTP/gRPC only where strong consistency is required.

| Pattern | When Used | Examples |
|---------|-----------|---------|
| **Async (Message Bus)** | Domain events, eventual consistency acceptable, fire-and-forget | `ContributionRecorded`, `LoanApproved`, `MemberJoined`, audit events, notification triggers |
| **Sync HTTP** | Query data owned by another service during a workflow that requires fresh data | Dissolution service querying Contributions + Loans for settlement calculation |
| **Sync gRPC** | Internal high-frequency service-to-service calls with strong typing needs | Identity token introspection (if needed beyond JWT), FundAdmin membership validation |

### Message Broker: RabbitMQ + MassTransit

**Decision**: **MassTransit** (v8.x) on top of **RabbitMQ** as the message broker.

#### Why MassTransit

| Feature | MassTransit | NServiceBus | Raw RabbitMQ |
|---------|-------------|-------------|--------------|
| **License** | Apache 2.0 (free) | Commercial ($2,000+/dev/year) | Apache 2.0 |
| **.NET 8 support** | First-class | First-class | Via client library |
| **Saga/state machine** | Built-in (Automatonymous) | Built-in | Manual implementation |
| **Outbox pattern** | Built-in EF Core transactional outbox | Built-in | Manual |
| **Retry/error handling** | Built-in retry, redelivery, dead-letter | Built-in | Manual |
| **Testing** | `InMemoryTransport` for unit tests | Built-in test transport | Manual mocking |
| **Serialization** | System.Text.Json, MessagePack | JSON, XML | Manual |
| **Observability** | OpenTelemetry integration | OpenTelemetry | Manual |
| **Community** | Large OSS community, active GitHub | Smaller (commercial focus) | Largest (raw protocol) |
| **Learning curve** | Moderate | Moderate | Steep (low-level) |

**Verdict**: MassTransit provides the best balance of features, cost, and community for a .NET 8 project. NServiceBus is excellent but the commercial license is unjustifiable for an early-stage project. Raw RabbitMQ requires rebuilding everything MassTransit provides for free.

#### MassTransit Configuration Approach

```
// Per-service setup pattern
services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    
    // Register consumers from the service's assembly
    x.AddConsumers(typeof(Program).Assembly);
    
    // Register sagas (Loans service)
    x.AddSagaStateMachine<LoanApprovalSaga, LoanApprovalSagaState>()
     .EntityFrameworkRepository(r =>
     {
         r.ConcurrencyMode = ConcurrencyMode.Optimistic;
         r.ExistingDbContext<LoansDbContext>();
         r.UsePostgres();
     });
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost");
        cfg.UseMessageRetry(r => r.Exponential(5, 
            TimeSpan.FromSeconds(1), 
            TimeSpan.FromMinutes(5), 
            TimeSpan.FromSeconds(5)));
        cfg.ConfigureEndpoints(context);
    });
    
    // Transactional outbox for EF Core
    x.AddEntityFrameworkOutbox<ContributionsDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });
});
```

#### Key Domain Events

| Event | Publisher | Consumers | Pattern |
|-------|-----------|-----------|---------|
| `FundCreated` | FundAdmin | Audit | Pub/Sub |
| `FundStateChanged` | FundAdmin | All services | Pub/Sub |
| `MemberJoined` | FundAdmin | Contributions, Loans, Notifications | Pub/Sub |
| `MemberRemoved` | FundAdmin | Contributions, Loans, Notifications | Pub/Sub |
| `ContributionDueGenerated` | Contributions | Notifications, Audit | Pub/Sub |
| `ContributionRecorded` | Contributions | Audit, Notifications, Dissolution (cache) | Pub/Sub |
| `LoanRequested` | Loans | Notifications, Audit | Pub/Sub |
| `LoanApproved` | Loans | Contributions (ledger), Notifications, Audit | Pub/Sub |
| `LoanDisbursed` | Loans | Contributions (ledger), Notifications, Audit | Pub/Sub |
| `RepaymentRecorded` | Loans | Contributions (interest income), Notifications, Audit | Pub/Sub |
| `LoanClosed` | Loans | Notifications, Audit | Pub/Sub |
| `VotingSessionOpened` | Loans | Notifications | Pub/Sub |
| `VoteCast` | Loans | Audit | Pub/Sub |
| `DissolutionInitiated` | Dissolution | FundAdmin, Notifications, Audit | Pub/Sub |
| `DissolutionConfirmed` | Dissolution | FundAdmin, Notifications, Audit | Pub/Sub |

#### Transactional Outbox Pattern

**Critical for financial correctness**: Domain events must be published reliably. Use MassTransit's built-in EF Core transactional outbox:

1. Domain operation + outbox message are written in the **same database transaction**.
2. A background delivery service reads from the outbox table and publishes to RabbitMQ.
3. If the publish fails, it retries. If the DB transaction rolls back, the outbox message is never published.

This guarantees **at-least-once delivery** without distributed transactions (2PC).

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **All synchronous (HTTP)** | Creates tight coupling; a downstream service outage cascades to the caller. Financial events (contribution recorded) must be reliably delivered even if Notifications or Audit is temporarily down. |
| **gRPC everywhere** | gRPC is excellent for sync calls but doesn't provide pub/sub semantics. Would need a separate message bus anyway for events. |
| **Apache Kafka** | Overkill for this scale (100 funds, 1,000 members). Kafka's strengths (log compaction, replay, massive throughput) aren't needed. RabbitMQ's queue-based model with MassTransit is simpler and sufficient. Kafka lacks native saga support. |
| **Azure Service Bus** | Cloud-vendor lock-in. The spec targets Docker-first local dev. RabbitMQ runs locally in Docker trivially. If moving to Azure later, MassTransit abstracts the transport — switching to ASB is a configuration change. |
| **MediatR for in-process events** | MediatR is for in-process pub/sub within a single service. It doesn't cross service boundaries. Useful *within* a service for CQRS, but not a replacement for inter-service messaging. **Recommendation**: Use MediatR for intra-service CQRS + MassTransit for inter-service events. |

### Risks & Gotchas

- **Message ordering**: RabbitMQ does not guarantee global ordering. For the fund lending domain, this is acceptable because each event is scoped to a fund and processed idempotently. If strict ordering per fund is needed, use a MassTransit message partition key set to `FundId`.
- **Eventual consistency**: Members may briefly see stale data after a cross-service event (e.g., contribution recorded but Notifications hasn't sent the receipt yet). Acceptable per spec: "notifications within 60 seconds."
- **Poison messages**: MassTransit's retry + dead-letter queue handles poison messages. Configure a `_error` queue per consumer and monitor it in production.
- **Outbox table growth**: The outbox table in each service's DB grows with every published event. Configure cleanup: MassTransit's `BusOutboxDeliveryService` removes delivered messages. Set retention to 7 days for debugging.

---

## 3. Data Isolation & Multi-Tenancy

### Decision

**Schema-per-service** in a single PostgreSQL instance, with **logical FundId scoping** enforced at the infrastructure level via EF Core global query filters.

```
PostgreSQL Instance
├── fundmanager_identity      (schema: identity)
├── fundmanager_fundadmin     (schema: fundadmin)
├── fundmanager_contributions (schema: contributions)
├── fundmanager_loans         (schema: loans)
├── fundmanager_dissolution   (schema: dissolution)
├── fundmanager_notifications (schema: notifications)
└── fundmanager_audit         (schema: audit)
```

### Why Not Database-Per-Service

| Factor | Schema-Per-Service | Database-Per-Service |
|--------|-------------------|---------------------|
| **Operational complexity** | Single backup, single connection string management | 7 separate databases to back up, monitor, manage |
| **Resource usage** | Shared buffer pool, shared connections | Each DB has its own resource overhead |
| **Cross-service queries (dev/debug)** | Possible via schema-qualified queries (not for production use) | Requires federated queries or ETL |
| **Isolation** | Full logical isolation; different schemas cannot accidentally join | Full physical isolation |
| **Migration to separate DBs** | Easy to promote schemas to separate databases later | Already separate |
| **MVP suitability** | Excellent — simple to manage for a small team | Overkill for MVP |

**Verdict**: Schema-per-service gives 95% of the isolation benefits of database-per-service with significantly lower operational overhead. The plan already specifies this approach. If a service needs independent scaling of its database later, it can be promoted to its own database — the EF Core DbContext is already scoped.

### Multi-Tenant Fund Isolation (FundId Scoping)

The system is **not multi-tenant in the traditional SaaS sense** (one DB per customer). Instead, **each fund is a logical tenant boundary** within each service's schema. The isolation requirement from the spec (Constitution Principle I): "cross-fund data is never mixed in ledgers or reports."

#### Implementation: EF Core 8 Global Query Filters

```csharp
// Base entity for all fund-scoped entities
public interface IFundScoped
{
    Guid FundId { get; }
}

// In DbContext.OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Auto-apply FundId filter to all IFundScoped entities
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(IFundScoped).IsAssignableFrom(entityType.ClrType))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var fundIdProperty = Expression.Property(parameter, nameof(IFundScoped.FundId));
            var fundIdValue = Expression.Property(
                Expression.Constant(this), 
                nameof(CurrentFundId));
            var filter = Expression.Lambda(
                Expression.Equal(fundIdProperty, fundIdValue), 
                parameter);
            
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}

public Guid CurrentFundId { get; set; } // Set from middleware/request context
```

#### FundId Injection Middleware

```csharp
// Middleware extracts FundId from route/header and sets it on DbContext
public class FundScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ContributionsDbContext dbContext)
    {
        if (context.Request.RouteValues.TryGetValue("fundId", out var fundIdObj) 
            && Guid.TryParse(fundIdObj?.ToString(), out var fundId))
        {
            dbContext.CurrentFundId = fundId;
            // Also validate that the authenticated user has access to this fund
        }
        await next(context);
    }
}
```

#### Defense in Depth: DB-Level Row-Level Security (Optional Enhancement)

For additional safety, PostgreSQL Row-Level Security (RLS) can be layered on top of EF Core filters:

```sql
-- Enable RLS on fund-scoped tables
ALTER TABLE contributions.contribution_due ENABLE ROW LEVEL SECURITY;

CREATE POLICY fund_isolation ON contributions.contribution_due
    USING (fund_id = current_setting('app.current_fund_id')::uuid);
```

The application sets `SET LOCAL app.current_fund_id = '<fundId>'` at the start of each connection/transaction. This provides a **database-level safety net** even if the application layer has a bug in the query filter.

**Recommendation for MVP**: EF Core global query filters only. Add RLS as a hardening step before production launch.

### EF Core 8 Multi-Tenancy Patterns

```csharp
// DbContext factory that sets FundId from the request
public class FundScopedDbContextFactory<TContext> : IDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _inner;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TContext CreateDbContext()
    {
        var context = _inner.CreateDbContext();
        if (context is IFundScopedDbContext fundScoped)
        {
            var fundId = _httpContextAccessor.HttpContext?
                .GetRouteValue("fundId")?.ToString();
            if (Guid.TryParse(fundId, out var parsed))
                fundScoped.CurrentFundId = parsed;
        }
        return context;
    }
}
```

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Database-per-fund (tenant)** | Would create 100+ databases at scale. Unmanageable for maintenance, migrations, backups. Only suited for large enterprise SaaS with strict regulatory isolation per tenant. |
| **Database-per-service** | Adds operational overhead without proportional benefit at MVP scale. Can be adopted later if needed. |
| **Single schema, shared tables** | No isolation between services at the DB level. Any service could accidentally read/write another service's tables. Violates data ownership. |
| **Discriminator column only (no global filters)** | Too error-prone. Every query must remember to add `.Where(x => x.FundId == fundId)`. A single missed filter leaks cross-fund data — unacceptable for a financial system. |

### Risks & Gotchas

- **Global query filters and `IgnoreQueryFilters()`**: EF Core allows bypassing global filters via `.IgnoreQueryFilters()`. This must be **banned in code reviews** for fund-scoped entities. Use a Roslyn analyzer or architectural test to enforce.
- **Migration coordination**: All services share one PostgreSQL instance. Schema migrations must be coordinated to avoid locking conflicts. Mitigated by: (a) each service migrates only its own schema, (b) run migrations sequentially at deploy time.
- **Reporting across funds (Platform Admin)**: Platform-level dashboards (fund count, aggregate stats) need cross-fund queries. Use a **dedicated read model** populated by events, or a specific query path that explicitly skips fund scope with proper authorization checks.
- **Connection pooling**: 7 services sharing one PostgreSQL instance → 7 connection pools. Size each pool appropriately (e.g., 20 connections per service = 140 total). PostgreSQL default `max_connections = 100` may need to be raised, or use PgBouncer for connection pooling at the DB level.

---

## 4. Saga / Process Manager Pattern

### Decision

Use **MassTransit Saga State Machines** (Automatonymous) for multi-service orchestration. Sagas are persisted in EF Core (PostgreSQL) with optimistic concurrency.

### Identified Sagas

#### 4.1 Loan Approval & Disbursement Saga

The most complex multi-step workflow in the system.

**Steps**:
1. `LoanRequested` → Validate eligibility (query FundAdmin for membership status + fund pool balance)
2. If voting enabled → Create `VotingSession` → Wait for `VotingSessionClosed` or timeout
3. Admin decision: `LoanApprovalDecided` (approve/reject)
4. If approved → `DisburseLoan` → Create disbursement `Transaction` in Contributions → `LoanDisbursed`
5. Notify borrower → `NotificationRequested`
6. Record audit entry → `AuditEventPublished`

**Compensation**:
- If disbursement fails (e.g., insufficient fund balance at commit time) → Saga publishes `LoanDisbursementFailed` → Loan status reverted to `PendingApproval` → Admin notified.

```csharp
public class LoanApprovalSaga : MassTransitStateMachine<LoanApprovalSagaState>
{
    public State AwaitingApproval { get; private set; }
    public State AwaitingVote { get; private set; }
    public State AwaitingDisbursement { get; private set; }
    public State Completed { get; private set; }
    public State Failed { get; private set; }

    public Event<LoanRequested> LoanRequested { get; private set; }
    public Event<VotingSessionClosed> VotingCompleted { get; private set; }
    public Event<LoanApprovalDecided> ApprovalDecided { get; private set; }
    public Event<LoanDisbursed> Disbursed { get; private set; }
    public Event<LoanDisbursementFailed> DisbursementFailed { get; private set; }

    public LoanApprovalSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => LoanRequested, x => x.CorrelateById(ctx => ctx.Message.LoanId));
        Event(() => ApprovalDecided, x => x.CorrelateById(ctx => ctx.Message.LoanId));
        Event(() => Disbursed, x => x.CorrelateById(ctx => ctx.Message.LoanId));

        Initially(
            When(LoanRequested)
                .Then(ctx => { /* Store loan details in saga state */ })
                .TransitionTo(AwaitingApproval));

        During(AwaitingApproval,
            When(ApprovalDecided, ctx => ctx.Message.Decision == LoanDecision.Approved)
                .Publish(ctx => new DisburseLoanCommand(ctx.Saga.LoanId, ctx.Saga.FundId, ctx.Saga.Principal))
                .TransitionTo(AwaitingDisbursement),
            When(ApprovalDecided, ctx => ctx.Message.Decision == LoanDecision.Rejected)
                .Publish(ctx => new LoanRejectedEvent(ctx.Saga.LoanId, ctx.Message.Reason))
                .TransitionTo(Completed));

        During(AwaitingDisbursement,
            When(Disbursed)
                .Publish(ctx => new NotifyBorrowerCommand(ctx.Saga.BorrowerId, "LoanDisbursed"))
                .TransitionTo(Completed),
            When(DisbursementFailed)
                .Publish(ctx => new RevertLoanStatusCommand(ctx.Saga.LoanId))
                .Publish(ctx => new NotifyAdminCommand(ctx.Saga.FundId, "DisbursementFailed"))
                .TransitionTo(Failed));
    }
}
```

#### 4.2 Monthly Contribution Cycle Saga

**Steps**:
1. Scheduler triggers `GenerateMonthlyDues` command
2. For each active fund → Generate `ContributionDue` records
3. Publish `ContributionDueGenerated` per member → Notifications subscribes
4. Schedule late-detection check (due date + grace period) → `CheckOverdueDues`
5. Schedule missed-detection check (month end) → `MarkMissedDues`

**Implementation**: This is simpler — a **process manager** rather than a full saga. Use MassTransit's `IRecurringSchedule` with Quartz.NET integration:

```csharp
services.AddMassTransit(x =>
{
    x.AddQuartzConsumers(); // For scheduled messages
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseInMemoryScheduler(); // Or Quartz with PostgreSQL persistence
    });
});
```

#### 4.3 Dissolution Settlement Saga

**Steps**:
1. Fund Admin initiates dissolution → `DissolutionInitiated`
2. FundAdmin service transitions fund to `Dissolving` state, blocks new activity
3. Dissolution service queries Contributions for total paid per member (sync HTTP call — needs exact figures)
4. Dissolution service queries Loans for outstanding balances per member (sync HTTP call)
5. Calculate settlement → Validate no negative payouts
6. If blocked → Publish `DissolutionBlocked` with details
7. If clear → Present settlement report → Await admin confirmation
8. Admin confirms → `DissolutionConfirmed` → Fund state → `Dissolved`

**Note**: Steps 3–4 use **synchronous HTTP** because settlement calculation requires **exact, consistent** financial data. Eventual consistency is not acceptable for calculating net payouts.

### Why MassTransit Sagas (not custom)

| Factor | MassTransit Saga | Custom Implementation |
|--------|-----------------|----------------------|
| **State persistence** | Built-in EF Core/Redis/MongoDB | Must build from scratch |
| **Concurrency** | Optimistic/pessimistic concurrency on saga state | Must handle manually |
| **Timeouts/scheduling** | Built-in `Schedule` and `RequestTimeout` | Must build with Quartz/Hangfire |
| **Retry/compensation** | Retry policies + dead-letter built in | Must build |
| **Testing** | `InMemoryTestHarness` | Must mock everything |
| **Correlation** | Declarative via `CorrelateById`/`CorrelateBy` | Must build routing logic |
| **Visualization** | State machine can be exported as a diagram | No tooling |

**Verdict**: Custom saga implementation is a significant engineering effort for questionable benefit. MassTransit sagas are production-proven, well-documented, and tightly integrated with the messaging infrastructure already chosen.

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Choreography-only (no orchestrator)** | Works for simple 2-step flows (record payment → notify). Fails for multi-step flows with compensation (loan approval → voting → disbursement → ledger → notification). Without an orchestrator, the "big picture" of a workflow is scattered across multiple consumers, making debugging and failure recovery very difficult. |
| **Temporal.io / Durable Task Framework** | Temporal is excellent for complex workflows but introduces a significant infrastructure dependency (Temporal server + workers). Overkill when MassTransit sagas cover the needed patterns. Would reconsider if workflows become significantly more complex post-MVP. |
| **NServiceBus Saga** | Would be the natural choice if NServiceBus were the message broker library. Rejected due to licensing cost (Section 2). |

### Risks & Gotchas

- **Saga state table growth**: Each active saga instance has a row in the database. Completed sagas should be marked `Final` and cleaned up periodically.
- **Concurrency on saga state**: Two events arriving simultaneously for the same correlation ID can cause concurrency conflicts. MassTransit handles this with optimistic concurrency + retry.
- **Timeout handling**: Long-running sagas (e.g., voting window is 24–72 hours) need scheduled timeouts. Use MassTransit's `Schedule` feature backed by Quartz.NET with PostgreSQL persistence. In-memory scheduling loses state on restart.
- **Saga versioning**: Changing a saga's states or events while instances are in-flight requires careful version management. Add backward-compatible states; never remove states that have active instances.

---

## 5. API Gateway

### Decision

**YARP (Yet Another Reverse Proxy)** as the API gateway, running as a .NET 8 ASP.NET Core application.

### YARP vs Ocelot Comparison

| Feature | YARP (Microsoft) | Ocelot |
|---------|------------------|--------|
| **Maintainer** | Microsoft (actively developed) | Community (slower updates) |
| **.NET 8 support** | Native, first-class | Supported but historically lags new .NET releases |
| **Performance** | Kestrel-based, very high throughput | Good but slightly lower than YARP |
| **Configuration** | JSON/code-based, hot-reload from config providers | JSON-based (ocelot.json) |
| **Rate limiting** | Use ASP.NET Core 8 built-in rate limiting middleware | Built-in but less flexible |
| **Authentication** | Full ASP.NET Core auth middleware (JWT, OpenIdConnect) | JWT support but more configuration friction |
| **Request aggregation** | Not built-in (implement via middleware) | Built-in aggregation feature |
| **Load balancing** | Round-robin, least-requests, random, power-of-two | Round-robin, least-connection |
| **Health checks** | ASP.NET Core health checks integration | Built-in |
| **Transforms** | Powerful request/response transforms API | Header transforms, claims transforms |
| **Extensibility** | Full ASP.NET Core middleware pipeline | Delegating handlers |
| **Production use** | Used internally by Microsoft (Azure, Xbox) | Community projects |
| **Documentation** | Excellent (Microsoft Docs) | Adequate |

### Why YARP

1. **Microsoft-backed**: Active development, aligned with .NET release cadence. YARP 2.x is production-ready for .NET 8.
2. **ASP.NET Core native**: The gateway is a standard ASP.NET Core app, so **all middleware works**: authentication, rate limiting, CORS, OpenTelemetry, health checks.
3. **Performance**: YARP is built on the same Kestrel pipeline as ASP.NET Core and achieves near-raw proxy performance.
4. **Rate limiting**: .NET 8 ships `Microsoft.AspNetCore.RateLimiting` middleware — integrates trivially with YARP. Ocelot's built-in rate limiting is less flexible.
5. **JWT validation**: Standard ASP.NET Core `AddJwtBearer()` middleware. No custom integration needed.
6. **Hot-reload configuration**: Routes can be loaded from `appsettings.json`, a database, or a config service and hot-reloaded without restart.

### Gateway Responsibilities

| Responsibility | Implementation |
|---------------|----------------|
| **Routing** | YARP route configuration mapping `/api/funds/**` → FundAdmin service, `/api/contributions/**` → Contributions service, etc. |
| **Authentication** | ASP.NET Core JWT Bearer middleware. Validate token signature (asymmetric RS256). Extract claims (userId, roles). |
| **Authorization (coarse)** | Route-level authorization policies (e.g., `/api/admin/**` requires PlatformAdmin role). Fine-grained RBAC is enforced at the service level. |
| **Rate limiting** | `Microsoft.AspNetCore.RateLimiting` — fixed window or sliding window per-client, per-endpoint. E.g., OTP endpoint: 5 requests/15 min. Payment endpoint: 30 requests/min. |
| **Request aggregation** | Custom middleware for the mobile dashboard (aggregates Fund summary + Contributions summary + Loans summary into one response). Keep this minimal — prefer client-side composition. |
| **CORS** | Standard ASP.NET Core CORS middleware. Allow web and mobile origins. |
| **Health checks** | Aggregate health of downstream services via `/health` endpoint. |
| **Request/Response transforms** | Add `X-Correlation-Id` header for distributed tracing. Strip internal headers. |

### Sample YARP Configuration

```json
{
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": { "Path": "/api/auth/{**catch-all}" }
      },
      "fundadmin-route": {
        "ClusterId": "fundadmin-cluster",
        "AuthorizationPolicy": "Authenticated",
        "Match": { "Path": "/api/funds/{**catch-all}" }
      },
      "contributions-route": {
        "ClusterId": "contributions-cluster",
        "AuthorizationPolicy": "Authenticated",
        "RateLimiterPolicy": "standard",
        "Match": { "Path": "/api/contributions/{**catch-all}" }
      },
      "loans-route": {
        "ClusterId": "loans-cluster",
        "AuthorizationPolicy": "Authenticated",
        "Match": { "Path": "/api/loans/{**catch-all}" }
      }
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": {
          "identity-1": { "Address": "http://localhost:5001" }
        }
      },
      "fundadmin-cluster": {
        "Destinations": {
          "fundadmin-1": { "Address": "http://localhost:5002" }
        }
      },
      "contributions-cluster": {
        "Destinations": {
          "contributions-1": { "Address": "http://localhost:5003" }
        }
      },
      "loans-cluster": {
        "Destinations": {
          "loans-1": { "Address": "http://localhost:5004" }
        }
      }
    }
  }
}
```

### Request Aggregation Strategy

**Decision**: Minimal aggregation at the gateway. Prefer **Backend-for-Frontend (BFF)** pattern for complex aggregations.

For the mobile dashboard that needs data from 3 services (fund summary, pending dues, active loans), two approaches were considered:

| Approach | Pros | Cons |
|----------|------|------|
| **Gateway aggregation (custom middleware)** | Single request from client; lower latency on slow mobile networks | Gateway becomes smart/thick; increased coupling; harder to test |
| **Client-side composition (parallel requests)** | Gateway stays thin; each call is independently cacheable | Multiple round trips from client; higher latency on slow networks |
| **BFF service** | Clean separation; aggregation logic is testable; gateway stays thin | Extra service to maintain |

**Decision**: Client-side composition for MVP (React Native TanStack Query makes parallel fetching trivial). Add a thin BFF aggregation endpoint in the gateway only if mobile performance profiling shows it's necessary.

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Ocelot** | Less actively maintained; .NET 8 support historically lags; rate limiting is less flexible than ASP.NET Core 8 native. Still viable but YARP is the better long-term bet. |
| **Envoy/Traefik (non-.NET)** | Powerful proxies but introduce a non-.NET technology. Configuration is in YAML/HCL, not C#. Loses the benefit of sharing authentication middleware and .NET observability. Would consider if moving to Kubernetes at scale. |
| **No gateway (direct client-to-service)** | Clients must manage 7 service URLs, handle auth per-service, and implement their own rate limiting. Unacceptable for mobile clients. |
| **Azure API Management / AWS API Gateway** | Cloud-native option. Vendor lock-in, not localhost-friendly for dev. Spec requires Docker-first local development. |

### Risks & Gotchas

- **Single point of failure**: The gateway is the single entry point. Mitigate with health checks, liveness probes, and horizontal scaling (multiple gateway instances behind a load balancer).
- **Gateway bloat**: Resist the temptation to add business logic to the gateway. It should only handle cross-cutting concerns (auth, rate limiting, routing, observability).
- **Request aggregation complexity**: If the BFF pattern is adopted, ensure the aggregation endpoints have proper timeouts and circuit breakers for downstream calls. Use `IHttpClientFactory` with Polly for resilience.

---

## 6. Financial Calculation Safety

### Decision

Use C# `decimal` type end-to-end with PostgreSQL `numeric(18,2)` storage, banker's rounding via `MidpointRounding.ToEven`, and centralized calculation utilities in `FundManager.BuildingBlocks`.

### C# `decimal` Type

The `decimal` type in C# is a **128-bit** fixed-point number with:
- **28–29 significant digits** of precision
- **No floating-point representation errors** for base-10 arithmetic
- **Exact** representation of values like `0.01`, `0.10`, `999999.99`

This makes it the **only appropriate type** for financial calculations in .NET. `float` and `double` use binary floating-point (IEEE 754) and cannot exactly represent most decimal fractions.

```csharp
// NEVER do this:
double interest = 50000.0 * 0.02; // = 999.9999999999999 (binary float error)

// ALWAYS do this:
decimal interest = 50000.00m * 0.02m; // = 1000.00 (exact)
```

### PostgreSQL `numeric(18,2)` Mapping

| C# Type | PostgreSQL Type | EF Core Mapping | Range |
|---------|-----------------|-----------------|-------|
| `decimal` | `numeric(18,2)` | Default convention + explicit config | ±9,999,999,999,999,999.99 |

```csharp
// In entity configuration
public class ContributionDueConfiguration : IEntityTypeConfiguration<ContributionDue>
{
    public void Configure(EntityTypeBuilder<ContributionDue> builder)
    {
        builder.Property(x => x.AmountDue)
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        
        builder.Property(x => x.AmountPaid)
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        
        builder.Property(x => x.RemainingBalance)
            .HasColumnType("numeric(18,2)")
            .IsRequired();
    }
}
```

### Why `numeric(18,2)` Specifically

- **18 digits total, 2 decimal places**: Supports values up to ~₹10 quadrillion with paisa precision. Far exceeds the needs of a community fund (typical fund pool: ₹1–50 lakh).
- **2 decimal places**: INR uses paise (1/100 of a rupee). The spec mandates "rounded to nearest paisa (2 decimal places)."
- **PostgreSQL `numeric` is exact**: Unlike `real`/`double precision`, PostgreSQL `numeric` stores values as exact decimal digits — no floating-point errors.

### Banker's Rounding (Round Half to Even)

The spec (Section 7.4) mandates banker's rounding. Implementation:

```csharp
// Centralized in FundManager.BuildingBlocks
public static class MoneyMath
{
    /// <summary>
    /// Rounds a monetary value to 2 decimal places using banker's rounding.
    /// Per spec Section 7.4: "round half to even to minimise cumulative bias."
    /// </summary>
    public static decimal RoundMoney(this decimal value)
        => Math.Round(value, 2, MidpointRounding.ToEven);

    /// <summary>
    /// Calculates monthly interest on outstanding principal.
    /// Per spec Section 7.1: interest_due = outstanding_principal × monthly_interest_rate
    /// </summary>
    public static decimal CalculateInterestDue(decimal outstandingPrincipal, decimal monthlyInterestRate)
        => (outstandingPrincipal * monthlyInterestRate).RoundMoney();

    /// <summary>
    /// Calculates principal due for a repayment.
    /// Per spec Section 7.1: principal_due = max(minimum_principal, scheduled_installment), 
    /// capped at outstanding_principal
    /// </summary>
    public static decimal CalculatePrincipalDue(
        decimal outstandingPrincipal,
        decimal minimumPrincipal,
        decimal scheduledInstallment)
    {
        var principalDue = Math.Max(minimumPrincipal, scheduledInstallment);
        return Math.Min(principalDue, outstandingPrincipal);
    }

    /// <summary>
    /// Distributes an interest pool proportionally by member weight.
    /// Per spec Section 7.3: interest_share = total_interest × (member_weight / total_weight)
    /// Remainder (from rounding) goes to the member with the highest weight.
    /// </summary>
    public static IReadOnlyDictionary<Guid, decimal> DistributeInterestPool(
        decimal totalInterestPool,
        IReadOnlyDictionary<Guid, decimal> memberWeights)
    {
        var totalWeight = memberWeights.Values.Sum();
        if (totalWeight == 0m) return memberWeights.ToDictionary(kv => kv.Key, _ => 0m);

        var shares = new Dictionary<Guid, decimal>();
        var distributedTotal = 0m;

        foreach (var (memberId, weight) in memberWeights)
        {
            var share = (totalInterestPool * weight / totalWeight).RoundMoney();
            shares[memberId] = share;
            distributedTotal += share;
        }

        // Assign rounding remainder to highest-weight member (spec Section 7.4)
        var remainder = totalInterestPool - distributedTotal;
        if (remainder != 0m)
        {
            var highestWeightMember = memberWeights
                .OrderByDescending(kv => kv.Value)
                .First().Key;
            shares[highestWeightMember] += remainder;
        }

        return shares;
    }
}
```

### EF Core Value Converters (Not Needed for decimal → numeric)

EF Core's Npgsql provider maps `decimal` to PostgreSQL `numeric` **natively** — no custom value converter is needed. However, ensure column type is explicitly set to `numeric(18,2)` to enforce precision at the database level:

```csharp
// Convention for all decimal properties (in DbContext)
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Properties<decimal>()
        .HaveColumnType("numeric(18,2)");
}
```

This applies the column type to **every `decimal` property** across all entities, ensuring no monetary field accidentally uses a different precision.

### Validation Rules

```csharp
// In FluentValidation validators
public class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .WithMessage("Payment amount must be positive")
            .ScalePrecision(2, 18)
            .WithMessage("Payment amount must have at most 2 decimal places");
    }
}
```

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Store amounts as `bigint` (paise/cents)** | Common in payment systems. Avoids decimal math at the DB level. Rejected because: (a) adds conversion logic at every boundary, (b) C# `decimal` is already exact, (c) the spec uses Rupee.Paise notation throughout, (d) PostgreSQL `numeric` is exact. The integer approach is a workaround for languages/DBs with poor decimal support — not needed here. |
| **`double` / `float`** | Binary floating-point. Cannot exactly represent `0.01`. Completely unsuitable for financial calculations. `50000.0 * 0.02` may not equal `1000.0`. |
| **`money` type in PostgreSQL** | PostgreSQL's `money` type is locale-dependent and has limited precision (8 bytes, ±92 trillion with 2 decimal places). `numeric` is more portable, explicit, and recommended by PostgreSQL documentation for financial data. |
| **Rounding mode: `MidpointRounding.AwayFromZero`** | "Normal" rounding (0.5 → 1). Introduces a systematic upward bias in aggregate calculations. For a fund with thousands of transactions, this bias accumulates. Banker's rounding (0.5 → nearest even) is statistically unbiased and is the standard for financial applications. |

### Risks & Gotchas

- **Implicit conversions**: Ensure no `double`/`float` sneaks into monetary calculations. A code review checklist or Roslyn analyzer can enforce `decimal` usage for monetary fields.
- **JSON serialization precision**: `System.Text.Json` serializes `decimal` as a JSON `number`. Ensure the client-side JavaScript uses a library like `big.js` or `decimal.js` for parsing, as JavaScript `Number` is a 64-bit float. Alternatively, serialize monetary values as **strings** in JSON responses:
  ```csharp
  [JsonConverter(typeof(DecimalToStringJsonConverter))]
  public decimal Amount { get; set; }
  ```
- **Rounding remainder in dissolution**: The `DistributeInterestPool` method assigns remainder to the highest-weight member. This is transparent and documented. An alternative is to distribute remainder to the fund's operating account.
- **Division operations**: `decimal / decimal` in C# preserves full precision. Always **round after the final calculation**, not at intermediate steps:
  ```csharp
  // WRONG: rounding at intermediate step
  var share = (totalInterest / totalWeight).RoundMoney() * memberWeight;
  
  // RIGHT: round only the final result
  var share = (totalInterest * memberWeight / totalWeight).RoundMoney();
  ```

---

## 7. Idempotency

### Decision

Implement idempotency for all payment/repayment endpoints using a **client-generated `Idempotency-Key` header** with server-side storage in the service's database, leveraging a unique constraint on the key.

### Pattern: Idempotency Key with Response Caching

```
Client Flow:
1. Client generates a UUID v4 as the Idempotency-Key
2. Client sends POST /api/contributions/{fundId}/dues/{dueId}/payments
   Headers: Idempotency-Key: <uuid>
3. Server checks if key exists:
   a. Key NOT found → Process payment → Store key + response → Return 201
   b. Key found, processing complete → Return cached response (200)
   c. Key found, processing in-progress → Return 409 Conflict (retry later)
```

### Implementation

```csharp
// Idempotency table (per service schema)
public class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = null!;  // Client-provided UUID
    public Guid FundId { get; set; }                       // Fund-scoped
    public string EndpointName { get; set; } = null!;      // e.g., "RecordContribution"
    public string? RequestHash { get; set; }               // SHA256 of request body
    public int ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }              // Cached JSON response
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public IdempotencyStatus Status { get; set; }          // Processing, Completed, Failed
}

public enum IdempotencyStatus { Processing, Completed, Failed }

// DB constraint
builder.HasIndex(x => new { x.FundId, x.IdempotencyKey })
    .IsUnique();
```

```csharp
// Middleware / Action Filter
public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly ContributionsDbContext _db;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers
            .TryGetValue("Idempotency-Key", out var keyHeader))
        {
            context.Result = new BadRequestObjectResult(
                "Idempotency-Key header is required for this endpoint");
            return;
        }

        var idempotencyKey = keyHeader.ToString();
        var fundId = GetFundIdFromRoute(context);

        // Check for existing record
        var existing = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.FundId == fundId && r.IdempotencyKey == idempotencyKey);

        if (existing is not null)
        {
            switch (existing.Status)
            {
                case IdempotencyStatus.Completed:
                    // Return cached response
                    context.Result = new ObjectResult(
                        JsonSerializer.Deserialize<object>(existing.ResponseBody!))
                    {
                        StatusCode = existing.ResponseStatusCode
                    };
                    return;

                case IdempotencyStatus.Processing:
                    // Another request is in-flight
                    context.Result = new ConflictObjectResult(
                        "This request is currently being processed. Please retry.");
                    return;

                case IdempotencyStatus.Failed:
                    // Previous attempt failed — allow retry by deleting the record
                    _db.IdempotencyRecords.Remove(existing);
                    await _db.SaveChangesAsync();
                    break;
            }
        }

        // Create processing record
        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            FundId = fundId,
            EndpointName = context.ActionDescriptor.DisplayName ?? "unknown",
            Status = IdempotencyStatus.Processing,
            CreatedAt = DateTime.UtcNow
        };

        _db.IdempotencyRecords.Add(record);
        await _db.SaveChangesAsync();

        // Execute the action
        var result = await next();

        // Cache the response
        if (result.Result is ObjectResult objectResult)
        {
            record.ResponseStatusCode = objectResult.StatusCode ?? 200;
            record.ResponseBody = JsonSerializer.Serialize(objectResult.Value);
            record.Status = IdempotencyStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            record.Status = IdempotencyStatus.Failed;
        }

        await _db.SaveChangesAsync();
    }
}
```

### Applying to Endpoints

```csharp
[HttpPost("{fundId}/dues/{dueId}/payments")]
[ServiceFilter(typeof(IdempotencyFilter))]
public async Task<IActionResult> RecordContribution(
    Guid fundId, Guid dueId, RecordPaymentCommand command)
{
    // Business logic — only executes once per idempotency key
    var result = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetTransaction), new { id = result.TransactionId }, result);
}
```

### Idempotent Batch Jobs (Monthly Dues Generation)

For the monthly contribution cycle and repayment generation (NFR-011):

```csharp
// Dues generation is idempotent by design: check-before-create
public async Task GenerateMonthlyDues(Guid fundId, int year, int month)
{
    var existingDues = await _db.ContributionDues
        .Where(d => d.FundId == fundId && d.Year == year && d.Month == month)
        .Select(d => d.MemberContributionPlanId)
        .ToHashSetAsync();

    var plans = await _db.MemberContributionPlans
        .Where(p => p.FundId == fundId && p.IsActive)
        .ToListAsync();

    var newDues = plans
        .Where(p => !existingDues.Contains(p.Id))
        .Select(p => new ContributionDue
        {
            Id = Guid.NewGuid(),
            FundId = fundId,
            MemberContributionPlanId = p.Id,
            Year = year,
            Month = month,
            AmountDue = p.MonthlyContribAmount,
            RemainingBalance = p.MonthlyContribAmount,
            Status = ContributionStatus.Pending,
            DueDate = CalculateDueDate(year, month, fundId),
            CreatedAt = DateTime.UtcNow
        })
        .ToList();

    if (newDues.Any())
    {
        _db.ContributionDues.AddRange(newDues);
        await _db.SaveChangesAsync();
    }
}
```

The key is the unique constraint on `(FundId, MemberContributionPlanId, Year, Month)` — attempting to generate the same due twice results in a constraint violation, making the operation naturally idempotent.

### Key Retention

Per spec Section 12: "Idempotency keys are stored and checked for 90 days." Implement a cleanup job:

```csharp
// Background service to clean old idempotency records
public class IdempotencyCleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
            
            var cutoff = DateTime.UtcNow.AddDays(-90);
            await db.IdempotencyRecords
                .Where(r => r.CreatedAt < cutoff)
                .ExecuteDeleteAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Redis-based idempotency** | Faster lookups but adds a dependency. If Redis is down, idempotency check fails open or blocks requests. Database-backed idempotency is transactional — the idempotency record and the business operation are written in the same DB transaction via EF Core. |
| **Idempotency at the message bus level** | MassTransit supports message deduplication. This helps for async message consumers but doesn't cover HTTP endpoints (where the client is waiting for a response). |
| **Conditional inserts (UPSERT)** | Using PostgreSQL `ON CONFLICT DO NOTHING` is simpler but doesn't return the original response to the client. The idempotency table approach caches the full response. |
| **No idempotency (rely on frontend)** | The spec (NFR-010) explicitly mandates server-side idempotency. Frontend deduplication is insufficient — network issues can cause retries. |

### Risks & Gotchas

- **Request body mismatch**: If the client sends the same idempotency key with a different request body, the server should detect this. Store a hash of the request body (`RequestHash`) and compare on duplicate key. Return a `422 Unprocessable Entity` if the body doesn't match.
- **Idempotency key scope**: Keys are scoped per-fund (unique constraint on `FundId + IdempotencyKey`). This prevents cross-fund collisions while allowing the same key to be reused across funds (unlikely but theoretically valid).
- **Processing timeout**: If a request starts processing (status = `Processing`) but the server crashes, the record is stuck. Implement a timeout: if `Processing` for > 5 minutes, treat as `Failed` and allow retry.
- **Concurrent retries**: Two identical requests arriving simultaneously may both see "no existing record" and both attempt to insert. The unique constraint catches this — one succeeds, the other gets a constraint violation and should retry after a brief delay.

---

## 8. Concurrency Control

### Decision

**Optimistic concurrency control** using EF Core 8's concurrency tokens, implemented as an auto-incrementing `Version` column (`xmin` system column in PostgreSQL or explicit `uint Version` with `[ConcurrencyCheck]`).

### Why Optimistic Concurrency

The spec (Section 2, Clarifications + FR-035a) mandates: "Optimistic locking — first write wins; second gets a conflict error and must refresh & retry."

The key scenario: Two users (Fund Admin + member) simultaneously record a payment for the same `ContributionDue`. Without concurrency control, both writes could succeed, double-paying the due.

| Approach | Pros | Cons | Verdict |
|----------|------|------|---------|
| **Optimistic (version-based)** | No long locks; high throughput; conflict is rare (most dues are paid by one person) | Client must handle retry | **Selected** |
| **Pessimistic (SELECT FOR UPDATE)** | Guaranteed serialization | Holds DB locks; reduces throughput; can deadlock | Rejected for API workloads |
| **Application-level distributed lock (Redis)** | Works across services | Adds Redis dependency; lock management complexity; timeout handling | Rejected for single-service operations |

### Implementation: EF Core Concurrency Token

#### Option A: PostgreSQL `xmin` System Column (Recommended)

PostgreSQL provides a hidden `xmin` column on every row that changes with each update. EF Core's Npgsql provider supports this natively:

```csharp
public class ContributionDue : IFundScoped
{
    public Guid Id { get; set; }
    public Guid FundId { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public ContributionStatus Status { get; set; }
    // ... other properties

    // xmin concurrency token — no explicit column needed, uses PostgreSQL system column
    [Timestamp]
    public uint Version { get; set; }
}

// In DbContext configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ContributionDue>()
        .UseXminAsConcurrencyToken();  // Npgsql-specific extension
}
```

#### Option B: Explicit `RowVersion` Column

If you prefer an explicit, portable version column:

```csharp
public class ContributionDue : IFundScoped
{
    // ... properties

    [ConcurrencyCheck]
    public int RowVersion { get; set; }
}

// In DbContext — manually increment on save
public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    foreach (var entry in ChangeTracker.Entries<ContributionDue>()
        .Where(e => e.State == EntityState.Modified))
    {
        entry.Entity.RowVersion++;
    }
    return base.SaveChangesAsync(cancellationToken);
}
```

**Recommendation**: Use `xmin` (Option A) for MVP. It requires no schema changes, no manual version management, and is supported natively by Npgsql. The generated SQL automatically includes `WHERE xmin = @originalXmin` in UPDATE statements.

### Handling Concurrency Conflicts

```csharp
public class RecordContributionPaymentHandler
{
    private readonly ContributionsDbContext _db;

    public async Task<PaymentResult> Handle(RecordPaymentCommand cmd)
    {
        var due = await _db.ContributionDues
            .FirstOrDefaultAsync(d => d.Id == cmd.DueId && d.FundId == cmd.FundId)
            ?? throw new NotFoundException("Contribution due not found");

        // Business logic
        due.AmountPaid += cmd.Amount;
        due.RemainingBalance = due.AmountDue - due.AmountPaid;
        due.Status = due.RemainingBalance <= 0m
            ? ContributionStatus.Paid
            : ContributionStatus.Partial;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            FundId = cmd.FundId,
            Amount = cmd.Amount,
            Type = TransactionType.Contribution,
            // ...
        };
        _db.Transactions.Add(transaction);

        try
        {
            await _db.SaveChangesAsync();
            return new PaymentResult(transaction.Id, due.Status, due.RemainingBalance);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another user modified this due between our read and write
            throw new ConcurrencyConflictException(
                "This contribution due was modified by another user. " +
                "Please refresh the data and try again.",
                entityId: due.Id,
                entityType: "ContributionDue");
        }
    }
}
```

### API Response for Concurrency Conflicts

```csharp
// Global exception handler
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is ConcurrencyConflictException conflict)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "CONCURRENCY_CONFLICT",
                message = conflict.Message,
                entityId = conflict.EntityId,
                entityType = conflict.EntityType,
                retryable = true
            });
        }
    });
});
```

### Entities Requiring Concurrency Control

| Entity | Why | Conflict Scenario |
|--------|-----|-------------------|
| **ContributionDue** | Two users recording payment for same due | Admin + member both click "pay" |
| **RepaymentEntry** | Two users recording repayment for same loan installment | Admin + borrower both click "pay" |
| **Loan** (status field) | Concurrent status transitions | Admin approves while another admin rejects |
| **Fund** (pool balance) | Concurrent disbursements | Two loan approvals depleting the pool simultaneously |
| **VotingSession** | Concurrent vote finalization | Two admins finalizing at the same time |

### Concurrent Loan Disbursement (Fund Pool Safety)

The spec (Edge Cases, Section 12): "Two simultaneous loan requests that would exceed the fund pool — sequential processing."

For the fund pool balance, optimistic concurrency alone isn't sufficient because the "check balance → deduct" operation must be atomic:

```csharp
// In Loans service — disbursement handler
public async Task<DisbursementResult> DisburseLoan(DisburseLoanCommand cmd)
{
    // Use a serializable transaction for fund pool operations
    await using var transaction = await _db.Database
        .BeginTransactionAsync(IsolationLevel.Serializable);

    try
    {
        var fund = await _db.Funds
            .FirstOrDefaultAsync(f => f.Id == cmd.FundId);

        if (fund.PoolBalance < cmd.Principal)
            throw new InsufficientFundsException(
                $"Fund pool balance ({fund.PoolBalance:C}) is less than loan principal ({cmd.Principal:C})");

        fund.PoolBalance -= cmd.Principal;

        var loan = await _db.Loans.FindAsync(cmd.LoanId);
        loan.Status = LoanStatus.Active;
        loan.DisbursementDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new DisbursementResult(loan.Id, cmd.Principal, fund.PoolBalance);
    }
    catch (Exception)
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

**Alternative for pool balance**: Use PostgreSQL advisory locks instead of serializable isolation:

```sql
-- Advisory lock scoped to fund ID
SELECT pg_advisory_xact_lock(hashtext(fund_id::text));
-- Now safely read and update pool balance
```

This is lower-overhead than serializable isolation but requires raw SQL or Npgsql-specific commands.

**Recommendation**: Use `Serializable` isolation for disbursement transactions (they're infrequent). Use optimistic concurrency (`xmin`) for high-frequency operations (payment recording).

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| **Pessimistic locking (SELECT FOR UPDATE)** | Appropriate for disbursement (chosen via Serializable isolation) but too heavy for payment recording where contention is rare. |
| **Last-write-wins** | Unacceptable for financial data. Could result in lost payments or double-counted balances. |
| **Event sourcing** | Would eliminate concurrency issues by appending events instead of updating state. But event sourcing adds significant complexity (event store, projections, snapshots) that isn't justified at this scale. Would consider post-MVP if audit requirements grow. |
| **Distributed lock (Redis/Redlock)** | Appropriate for cross-service operations but unnecessary here — all concurrent writes for a single entity happen within one service's database. |

### Risks & Gotchas

- **Stale reads in the UI**: After a 409 Conflict, the client must re-fetch the current state before retrying. The API should return the entity's current version in the response (or the client should re-GET it).
- **Version column in DTOs**: Include the version/ETag in API responses so clients can send it back on updates. Use the `ETag`/`If-Match` HTTP headers for RESTful design:
  ```
  GET /api/contributions/funds/{fundId}/dues/{dueId}
  Response: { ..., "version": 42 }
  Headers: ETag: "42"

  PUT /api/contributions/funds/{fundId}/dues/{dueId}/payments
  Headers: If-Match: "42"
  ```
- **xmin is transient**: PostgreSQL `xmin` values are transaction IDs that wrap around. For very long-lived rows, this is fine because the concurrency check is only relevant within the window of concurrent modifications. But for API caching (ETag), prefer an explicit `RowVersion` integer that monotonically increases.
- **Retry storms**: If many clients retry simultaneously after a conflict, it could create a cascade. Implement **exponential backoff with jitter** on the client side. The API should return a `Retry-After` header with a suggested delay.

---

## Summary of Decisions

| Area | Decision | Key Technology |
|------|----------|---------------|
| **Service decomposition** | 7 bounded-context services + API gateway | .NET 8 Clean Architecture per service |
| **Inter-service communication** | Async (MassTransit + RabbitMQ) default; sync HTTP for strong-consistency queries | MassTransit 8.x, transactional outbox |
| **Data isolation** | Schema-per-service in single PostgreSQL; FundId scoping via EF Core global query filters | EF Core 8, Npgsql, PostgreSQL 16 |
| **Saga / orchestration** | MassTransit state machine sagas for multi-step workflows | Automatonymous, EF Core persistence |
| **API gateway** | YARP with ASP.NET Core 8 middleware | YARP 2.x, built-in rate limiting, JWT auth |
| **Financial calculations** | C# `decimal` + PostgreSQL `numeric(18,2)` + banker's rounding | Centralized `MoneyMath` utilities |
| **Idempotency** | Client-generated `Idempotency-Key` header + DB-backed dedup table | Per-service idempotency records, 90-day retention |
| **Concurrency control** | Optimistic (`xmin` concurrency token) for payments; Serializable isolation for disbursements | EF Core concurrency tokens, Npgsql `UseXminAsConcurrencyToken` |

---

## Open Questions for Design Phase

1. **Event schema evolution**: How to handle backward-incompatible event schema changes? Propose: use a `SchemaVersion` field on all events + a compatibility matrix in `FundManager.Contracts`.
2. **Testcontainers for integration tests**: Use Testcontainers for PostgreSQL + RabbitMQ in CI/CD. Verify: does the CI environment (GitHub Actions) support Docker-in-Docker?
3. **Offline mobile queue**: When a mobile client queues a payment while offline and syncs later, the idempotency key protects against duplicates — but should the due's staleness be checked? The offline payment may conflict with an Admin recording the same payment in the meantime.
4. **Rate limiting granularity**: Per-user, per-fund, per-IP, or per-endpoint? Start with per-user per-endpoint for MVP.
5. **Blue/green deployment strategy**: How to handle in-flight saga instances during deployment? MassTransit saga state must be backward-compatible across versions.
