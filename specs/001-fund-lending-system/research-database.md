# Database Research: PostgreSQL 16 for Fund Management System

**Date**: 2026-02-20  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Backend Research**: [research.md](research.md)  
**Scope**: PostgreSQL-specific design decisions for 7 microservice schemas within a single PostgreSQL 16 instance

---

## Table of Contents

1. [Column Type Decisions](#1-column-type-decisions)
2. [Schema DDL Design Per Service](#2-schema-ddl-design-per-service)
3. [Enum Strategy](#3-enum-strategy)
4. [EF Core Migration Strategy](#4-ef-core-migration-strategy)
5. [Indexing Deep-Dive](#5-indexing-deep-dive)
6. [Connection Pooling](#6-connection-pooling)
7. [Table Partitioning](#7-table-partitioning)
8. [Backup, Retention & Archival](#8-backup-retention--archival)
9. [PostgreSQL Extensions](#9-postgresql-extensions)
10. [Performance Tuning](#10-performance-tuning)
11. [Encryption at Rest](#11-encryption-at-rest)
12. [Idempotency Table Design](#12-idempotency-table-design)
13. [Audit Table Design](#13-audit-table-design)
14. [Summary of Decisions](#14-summary-of-decisions)

---

## 1. Column Type Decisions

### Decision

Standardised column type mapping for all entities across all 7 schemas.

### Type Matrix

| Domain Concept | C# Type | PostgreSQL Type | Rationale |
|---------------|---------|-----------------|-----------|
| Primary Key (all entities) | `Guid` | `uuid` | Globally unique across services; no coordination needed. Generated client-side via `Guid.NewGuid()` (UUIDv4) |
| Foreign Key references | `Guid` | `uuid` | Matches PK type |
| Monetary values (amounts, balances) | `decimal` | `numeric(18,2)` | Exact decimal arithmetic; 2 decimal places for INR paisa; range ±9,999,999,999,999,999.99 |
| Interest rates | `decimal` | `numeric(8,6)` | 6 decimal places for rates like 0.020000 (2%); range ±99.999999% — supports sub-basis-point precision |
| Percentages (voting threshold) | `decimal` | `numeric(5,2)` | Range 0.00–100.00 |
| Status fields | `string` (enum name) | `varchar(30)` | See [Enum Strategy](#3-enum-strategy) — string storage for forward compatibility |
| Short text (names, purposes) | `string` | `varchar(255)` | Bounded length prevents bloat |
| Long text (descriptions, reasons) | `string` | `text` | Unbounded, PostgreSQL handles efficiently |
| Timestamps | `DateTime` (UTC) | `timestamptz` | Always stored as UTC; PostgreSQL `timestamptz` stores UTC and converts on display. Per spec: UTC storage / IST display |
| Date-only (month/year) | `DateOnly` | `date` | For contribution due month, repayment month — no time component needed |
| Month/Year composite | `int` (YYYYMM) | `integer` | E.g., 202602 — enables efficient range queries and partitioning keys |
| Boolean flags | `bool` | `boolean` | Native PostgreSQL boolean |
| JSON payloads (audit before/after, notification placeholders) | `JsonDocument` or strongly-typed | `jsonb` | Binary JSON; supports indexing, querying, and efficient storage |
| Idempotency keys | `string` | `varchar(64)` | Client-generated UUID string; 36 chars typical, 64 max for safety |
| Row version (optimistic concurrency) | `uint` (xmin) | `xid` (system column) | PostgreSQL system column `xmin` used as concurrency token — no schema column needed |
| IP Address | `string` | `inet` | Native PostgreSQL `inet` type for audit logs |
| Enum arrays (notification channels) | `string[]` | `text[]` | PostgreSQL native arrays for multi-select fields |

### Why NOT `money` Type

PostgreSQL has a `money` type, but it is **rejected** for this project:

- `money` is **locale-dependent** — its formatting varies by `lc_monetary` setting
- `money` has **fixed 2-decimal-place precision** but the rounding behavior is locale-dependent
- `money` has **no scale parameter** — you cannot specify precision
- `money` does **not support arithmetic with `numeric`** without explicit casting
- EF Core / Npgsql **does not natively map** C# `decimal` to `money`

`numeric(18,2)` provides **exact, locale-independent, deterministic** arithmetic — the only acceptable choice for a financial system (per Constitution Principle II).

### Why UUIDv4 for Primary Keys

| Consideration | UUID (v4) | `bigserial` (auto-increment) |
|--------------|-----------|------------------------------|
| **Cross-service uniqueness** | Globally unique without coordination | Requires per-service sequences; collision risk if services merge |
| **Client-side generation** | Yes — `Guid.NewGuid()` before DB insert | No — must round-trip to DB for ID |
| **Index performance** | Slightly worse for B-tree (random distribution) | Better B-tree locality (sequential) |
| **Insert performance** | No contention on sequence | Sequence can be a bottleneck under high concurrency |
| **Security** | Non-guessable (random) | Sequential — enumeration attack risk on APIs |
| **Size** | 16 bytes | 8 bytes |

**Decision**: UUIDv4 for all primary keys. The index performance downside is mitigated by:
1. Using `uuid_generate_v7()` (time-ordered UUIDs) via `pg_uuidv7` extension when available, which provides B-tree locality similar to `bigserial` while retaining global uniqueness
2. MVP scale (100 funds × 1,000 members = 100K members max) — UUID index overhead is negligible

**EF Core configuration**:
```csharp
// In each entity configuration
builder.Property(x => x.Id)
    .HasColumnType("uuid")
    .HasDefaultValueSql("gen_random_uuid()") // PostgreSQL 13+ built-in
    .ValueGeneratedNever(); // We generate client-side in .NET
```

### Timestamp Handling

```csharp
// Global convention in DbContext.OnModelCreating
foreach (var property in modelBuilder.Model.GetEntityTypes()
    .SelectMany(t => t.GetProperties())
    .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
{
    property.SetColumnType("timestamptz");
}
```

**Npgsql 8.x behaviour**: Npgsql 6+ maps `DateTime` to `timestamptz` by default and enforces `DateTime.Kind == Utc`. Non-UTC `DateTime` values throw at runtime. This is desirable — it prevents accidental IST storage.

---

## 2. Schema DDL Design Per Service

### Decision

Each microservice owns one PostgreSQL schema. Tables are created via EF Core Code-First migrations. Below are the logical DDL designs for reference — EF Core generates the actual migration SQL.

### 2.1 Identity Schema (`identity`)

```sql
CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE identity.users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(255) NOT NULL,
    phone           varchar(20),           -- E.164 format, e.g., +919876543210
    email           varchar(255),
    profile_picture_url text,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    
    CONSTRAINT ck_users_contact CHECK (phone IS NOT NULL OR email IS NOT NULL)
);

CREATE TABLE identity.otp_challenges (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid REFERENCES identity.users(id),
    channel         varchar(10) NOT NULL,  -- 'phone' or 'email'
    target          varchar(255) NOT NULL, -- phone number or email
    otp_hash        varchar(128) NOT NULL, -- bcrypt/argon2 hash, NOT plaintext
    expires_at      timestamptz NOT NULL,
    verified        boolean NOT NULL DEFAULT false,
    attempts        integer NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE identity.sessions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES identity.users(id),
    token_hash      varchar(128) NOT NULL, -- hashed session token
    expires_at      timestamptz NOT NULL,
    revoked         boolean NOT NULL DEFAULT false,
    ip_address      inet,
    user_agent      text,
    created_at      timestamptz NOT NULL DEFAULT now()
);
```

### 2.2 Fund Admin Schema (`fundadmin`)

```sql
CREATE SCHEMA IF NOT EXISTS fundadmin;

CREATE TABLE fundadmin.funds (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name                        varchar(255) NOT NULL,
    description                 text,
    currency                    varchar(3) NOT NULL DEFAULT 'INR',
    monthly_interest_rate       numeric(8,6) NOT NULL,  -- e.g., 0.020000 for 2%
    minimum_monthly_contribution numeric(18,2) NOT NULL,
    minimum_principal_per_repayment numeric(18,2) NOT NULL DEFAULT 1000.00,
    loan_approval_policy        varchar(30) NOT NULL DEFAULT 'AdminOnly', -- AdminOnly, AdminWithVoting
    max_loan_per_member         numeric(18,2),           -- NULL = no cap
    max_concurrent_loans        integer,                  -- NULL = no cap
    dissolution_policy          text,
    overdue_penalty_type        varchar(20) DEFAULT 'None', -- None, Flat, Percentage
    overdue_penalty_value       numeric(18,2) DEFAULT 0.00,
    contribution_day_of_month   integer NOT NULL DEFAULT 1,
    grace_period_days           integer NOT NULL DEFAULT 5,
    state                       varchar(20) NOT NULL DEFAULT 'Draft', -- Draft, Active, Dissolving, Dissolved
    created_at                  timestamptz NOT NULL DEFAULT now(),
    updated_at                  timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_funds_interest_rate CHECK (monthly_interest_rate > 0 AND monthly_interest_rate <= 1.000000),
    CONSTRAINT ck_funds_min_contribution CHECK (minimum_monthly_contribution > 0),
    CONSTRAINT ck_funds_min_principal CHECK (minimum_principal_per_repayment > 0),
    CONSTRAINT ck_funds_state CHECK (state IN ('Draft', 'Active', 'Dissolving', 'Dissolved')),
    CONSTRAINT ck_funds_contribution_day CHECK (contribution_day_of_month BETWEEN 1 AND 28),
    CONSTRAINT ck_funds_grace_period CHECK (grace_period_days >= 0)
);

CREATE TABLE fundadmin.fund_role_assignments (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL,         -- references identity.users (cross-service reference by ID)
    fund_id         uuid NOT NULL REFERENCES fundadmin.funds(id),
    role            varchar(20) NOT NULL,  -- Admin, Editor, Guest
    assigned_at     timestamptz NOT NULL DEFAULT now(),
    assigned_by     uuid NOT NULL,         -- references identity.users

    CONSTRAINT uq_fund_role_user UNIQUE (user_id, fund_id),
    CONSTRAINT ck_role CHECK (role IN ('Admin', 'Editor', 'Guest'))
);

CREATE TABLE fundadmin.member_contribution_plans (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                     uuid NOT NULL,
    fund_id                     uuid NOT NULL REFERENCES fundadmin.funds(id),
    monthly_contribution_amount numeric(18,2) NOT NULL,
    join_date                   date NOT NULL DEFAULT CURRENT_DATE,
    is_active                   boolean NOT NULL DEFAULT true,
    created_at                  timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_member_fund UNIQUE (user_id, fund_id),
    CONSTRAINT ck_contribution_amount CHECK (monthly_contribution_amount > 0)
);

CREATE TABLE fundadmin.invitations (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    fund_id         uuid NOT NULL REFERENCES fundadmin.funds(id),
    target_contact  varchar(255) NOT NULL, -- phone or email
    invited_by      uuid NOT NULL,
    status          varchar(20) NOT NULL DEFAULT 'Pending', -- Pending, Accepted, Declined, Expired
    expires_at      timestamptz NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    responded_at    timestamptz
);
```

### 2.3 Contributions Schema (`contributions`)

```sql
CREATE SCHEMA IF NOT EXISTS contributions;

CREATE TABLE contributions.contribution_dues (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    fund_id                 uuid NOT NULL,   -- cross-service FK (logical, not enforced)
    member_plan_id          uuid NOT NULL,   -- references fundadmin.member_contribution_plans
    user_id                 uuid NOT NULL,   -- denormalised for query convenience
    month_year              integer NOT NULL, -- YYYYMM format, e.g., 202602
    amount_due              numeric(18,2) NOT NULL,
    amount_paid             numeric(18,2) NOT NULL DEFAULT 0.00,
    remaining_balance       numeric(18,2) NOT NULL,
    status                  varchar(20) NOT NULL DEFAULT 'Pending',
    due_date                date NOT NULL,
    paid_date               timestamptz,
    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_contribution_due UNIQUE (user_id, fund_id, month_year),
    CONSTRAINT ck_status CHECK (status IN ('Pending', 'Paid', 'Partial', 'Late', 'Missed')),
    CONSTRAINT ck_amounts CHECK (amount_due >= 0 AND amount_paid >= 0 AND remaining_balance >= 0)
);

CREATE TABLE contributions.transactions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    fund_id             uuid NOT NULL,
    user_id             uuid NOT NULL,
    type                varchar(30) NOT NULL, -- Contribution, Disbursement, Repayment, InterestIncome, Penalty, Settlement
    amount              numeric(18,2) NOT NULL,
    idempotency_key     varchar(64) NOT NULL,
    reference_entity_type varchar(30),        -- ContributionDue, RepaymentEntry, Loan, etc.
    reference_entity_id uuid,
    recorded_by         uuid NOT NULL,
    description         text,
    created_at          timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_idempotency UNIQUE (fund_id, idempotency_key),
    CONSTRAINT ck_transaction_type CHECK (type IN ('Contribution', 'Disbursement', 'Repayment', 'InterestIncome', 'Penalty', 'Settlement'))
);
```

### 2.4 Loans Schema (`loans`)

```sql
CREATE SCHEMA IF NOT EXISTS loans;

CREATE TABLE loans.loans (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    fund_id                 uuid NOT NULL,
    borrower_id             uuid NOT NULL,
    principal_amount        numeric(18,2) NOT NULL,
    outstanding_principal   numeric(18,2) NOT NULL,
    monthly_interest_rate   numeric(8,6) NOT NULL,  -- snapshot from fund at disbursement
    scheduled_installment   numeric(18,2) NOT NULL DEFAULT 0.00,
    minimum_principal       numeric(18,2) NOT NULL DEFAULT 1000.00, -- snapshot from fund
    requested_start_month   integer NOT NULL,        -- YYYYMM
    purpose                 text,
    status                  varchar(30) NOT NULL DEFAULT 'PendingApproval',
    approved_by             uuid,
    rejection_reason        text,
    approval_date           timestamptz,
    disbursement_date       timestamptz,
    closed_date             timestamptz,
    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_loan_status CHECK (status IN ('PendingApproval', 'Approved', 'Active', 'Closed', 'Rejected')),
    CONSTRAINT ck_principal CHECK (principal_amount > 0),
    CONSTRAINT ck_outstanding CHECK (outstanding_principal >= 0),
    CONSTRAINT ck_installment CHECK (scheduled_installment >= 0)
);

CREATE TABLE loans.repayment_entries (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id         uuid NOT NULL REFERENCES loans.loans(id),
    fund_id         uuid NOT NULL,           -- denormalised for FundId scoping
    month_year      integer NOT NULL,        -- YYYYMM
    interest_due    numeric(18,2) NOT NULL,
    principal_due   numeric(18,2) NOT NULL,
    total_due       numeric(18,2) NOT NULL,
    amount_paid     numeric(18,2) NOT NULL DEFAULT 0.00,
    status          varchar(20) NOT NULL DEFAULT 'Pending',
    due_date        date NOT NULL,
    paid_date       timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_repayment_loan_month UNIQUE (loan_id, month_year),
    CONSTRAINT ck_repayment_status CHECK (status IN ('Pending', 'Paid', 'Partial', 'Overdue')),
    CONSTRAINT ck_repayment_amounts CHECK (interest_due >= 0 AND principal_due >= 0 AND total_due >= 0 AND amount_paid >= 0)
);

CREATE TABLE loans.voting_sessions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    loan_id             uuid NOT NULL REFERENCES loans.loans(id),
    fund_id             uuid NOT NULL,
    voting_window_start timestamptz NOT NULL,
    voting_window_end   timestamptz NOT NULL,
    threshold_type      varchar(20) NOT NULL DEFAULT 'Majority', -- Majority, Percentage
    threshold_value     numeric(5,2) NOT NULL DEFAULT 50.00,
    result              varchar(20) NOT NULL DEFAULT 'Pending',  -- Pending, Approved, Rejected, NoQuorum
    finalised_by        uuid,
    finalised_date      timestamptz,
    override_used       boolean NOT NULL DEFAULT false,
    created_at          timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_voting_loan UNIQUE (loan_id),
    CONSTRAINT ck_voting_result CHECK (result IN ('Pending', 'Approved', 'Rejected', 'NoQuorum')),
    CONSTRAINT ck_voting_window CHECK (voting_window_end > voting_window_start)
);

CREATE TABLE loans.votes (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    voting_session_id   uuid NOT NULL REFERENCES loans.voting_sessions(id),
    voter_id            uuid NOT NULL,
    decision            varchar(10) NOT NULL, -- Approve, Reject
    cast_at             timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_vote_session_voter UNIQUE (voting_session_id, voter_id),
    CONSTRAINT ck_vote_decision CHECK (decision IN ('Approve', 'Reject'))
);
```

### 2.5 Dissolution Schema (`dissolution`)

```sql
CREATE SCHEMA IF NOT EXISTS dissolution;

CREATE TABLE dissolution.dissolution_settlements (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    fund_id                     uuid NOT NULL,
    total_interest_pool         numeric(18,2) NOT NULL DEFAULT 0.00,
    total_contributions_collected numeric(18,2) NOT NULL DEFAULT 0.00,
    settlement_date             date,
    status                      varchar(20) NOT NULL DEFAULT 'Calculating', -- Calculating, Reviewed, Confirmed
    confirmed_by                uuid,
    created_at                  timestamptz NOT NULL DEFAULT now(),
    updated_at                  timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_dissolution_fund UNIQUE (fund_id),
    CONSTRAINT ck_dissolution_status CHECK (status IN ('Calculating', 'Reviewed', 'Confirmed'))
);

CREATE TABLE dissolution.dissolution_line_items (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    settlement_id               uuid NOT NULL REFERENCES dissolution.dissolution_settlements(id),
    user_id                     uuid NOT NULL,
    total_paid_contributions    numeric(18,2) NOT NULL DEFAULT 0.00,
    interest_share              numeric(18,2) NOT NULL DEFAULT 0.00,
    outstanding_loan_principal  numeric(18,2) NOT NULL DEFAULT 0.00,
    unpaid_interest             numeric(18,2) NOT NULL DEFAULT 0.00,
    unpaid_dues                 numeric(18,2) NOT NULL DEFAULT 0.00,
    gross_payout                numeric(18,2) NOT NULL DEFAULT 0.00,
    net_payout                  numeric(18,2) NOT NULL DEFAULT 0.00,

    CONSTRAINT uq_line_item_user UNIQUE (settlement_id, user_id)
);
```

### 2.6 Notifications Schema (`notifications`)

```sql
CREATE SCHEMA IF NOT EXISTS notifications;

CREATE TABLE notifications.notifications (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    recipient_id    uuid NOT NULL,
    fund_id         uuid,                    -- nullable for platform-level notifications
    channel         varchar(10) NOT NULL,    -- push, email, sms, in_app
    template_key    varchar(100) NOT NULL,
    placeholders    jsonb NOT NULL DEFAULT '{}',
    status          varchar(20) NOT NULL DEFAULT 'Pending', -- Pending, Sent, Failed
    retry_count     integer NOT NULL DEFAULT 0,
    max_retries     integer NOT NULL DEFAULT 3,
    next_retry_at   timestamptz,
    scheduled_at    timestamptz NOT NULL DEFAULT now(),
    sent_at         timestamptz,
    failed_at       timestamptz,
    failure_reason  text,
    created_at      timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_notification_channel CHECK (channel IN ('push', 'email', 'sms', 'in_app')),
    CONSTRAINT ck_notification_status CHECK (status IN ('Pending', 'Sent', 'Failed'))
);

CREATE TABLE notifications.notification_preferences (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL,
    channel         varchar(10) NOT NULL,
    enabled         boolean NOT NULL DEFAULT true,
    updated_at      timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_user_channel_pref UNIQUE (user_id, channel)
);

CREATE TABLE notifications.device_tokens (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL,
    device_id       varchar(255) NOT NULL,
    push_token      text NOT NULL,
    platform        varchar(10) NOT NULL,    -- ios, android
    updated_at      timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT uq_user_device UNIQUE (user_id, device_id)
);
```

### 2.7 Audit Schema (`audit`)

```sql
CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE audit.audit_logs (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_id        uuid NOT NULL,
    fund_id         uuid,                    -- nullable for platform-level actions
    timestamp       timestamptz NOT NULL DEFAULT now(),
    action_type     varchar(50) NOT NULL,    -- e.g., 'Fund.Created', 'Loan.Approved', 'Payment.Recorded'
    entity_type     varchar(50) NOT NULL,    -- e.g., 'Fund', 'Loan', 'ContributionDue'
    entity_id       uuid NOT NULL,
    before_state    jsonb,                   -- NULL for CREATE actions
    after_state     jsonb NOT NULL,
    ip_address      inet,
    user_agent      text,
    correlation_id  uuid,                    -- links related actions across services
    service_name    varchar(50) NOT NULL     -- which microservice emitted this
);

-- Append-only: no UPDATE or DELETE triggers
-- Enforced by revoking UPDATE/DELETE privileges on the table
```

### Cross-Service References

Cross-service foreign keys are **NOT enforced** at the database level (different schemas owned by different services). Instead:

| Reference | Source Schema | Target Schema | Enforcement |
|-----------|--------------|---------------|-------------|
| `contribution_dues.user_id` | contributions | identity | Application-level validation; event-driven sync |
| `contribution_dues.fund_id` | contributions | fundadmin | Application-level; FundId injected from trusted middleware |
| `loans.borrower_id` | loans | identity | Application-level validation |
| `loans.fund_id` | loans | fundadmin | Application-level |
| `audit_logs.actor_id` | audit | identity | Best-effort; audit must not fail if user data is inconsistent |

**Rationale**: Cross-schema FKs would create tight coupling between services and prevent independent schema migrations. The FundId middleware (from [research.md](research.md) Section 3) ensures fund context is always valid before reaching the service layer.

---

## 3. Enum Strategy

### Decision

Store enum values as **varchar strings** (the enum name) rather than PostgreSQL `CREATE TYPE` enums or integer codes.

### Comparison

| Approach | Pros | Cons |
|----------|------|------|
| **`varchar` string** | Forward-compatible (add new values without migration); human-readable in queries; EF Core default for string enums | Slightly more storage (27 bytes for "PendingApproval" vs 4 bytes for int) |
| **PostgreSQL `CREATE TYPE` enum** | Type-safe at DB level; compact storage | Adding new values requires `ALTER TYPE ... ADD VALUE` migration; cannot remove values; ordering matters; EF Core mapping is awkward |
| **`integer` code** | Most compact (4 bytes) | Not human-readable in raw SQL queries; requires a lookup table or code knowledge; brittle — changing integer assignments breaks data |

**Decision**: `varchar` strings. The storage overhead is negligible at MVP scale (100K rows × 15 extra bytes = ~1.5 MB). The operational benefits — no migration for new status values, human-readable audit logs and debug queries — far outweigh the cost.

**EF Core configuration**:
```csharp
// Global convention for all enum properties
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Properties<ContributionStatus>()
        .HaveConversion<string>()
        .HaveMaxLength(30);
    
    configurationBuilder.Properties<LoanStatus>()
        .HaveConversion<string>()
        .HaveMaxLength(30);
    
    // ... repeat for all enum types
}
```

**CHECK constraints** (shown in DDL above) provide database-level validation as a safety net.

---

## 4. EF Core Migration Strategy

### Decision

**Code-First migrations** with EF Core 8, following a structured naming convention and strict rules for financial tables.

### Migration Workflow

```text
1. Developer modifies entity / configuration
2. Run: dotnet ef migrations add <MigrationName> --project src/services/<Service>/Infrastructure --startup-project src/services/<Service>/Api
3. Review generated migration SQL (mandatory for financial tables)
4. Commit migration files to source control
5. At deployment: dotnet ef database update (or run via migration script in Docker entrypoint)
```

### Naming Convention

```
YYYYMMDD_HHMMSS_<ServiceShortName>_<Description>

Examples:
20260220_143000_Contributions_InitialSchema
20260221_091500_Loans_AddScheduledInstallmentColumn
20260222_160000_FundAdmin_AddMinPrincipalField
```

### Rules

1. **One migration per schema change**: Never combine multiple logical changes into one migration
2. **Review SQL for financial tables**: Before committing, run `dotnet ef migrations script` and review the generated SQL for:
   - Correct `numeric(18,2)` types on monetary columns
   - Proper CHECK constraints
   - No accidental `DROP COLUMN` on financial data
3. **No destructive migrations on production tables**: Never `DROP COLUMN` or `ALTER COLUMN` to a narrower type on tables with financial data (Transaction, ContributionDue, RepaymentEntry, Loan). Instead, add new columns and deprecate old ones
4. **Idempotent migrations**: EF Core migration history table (`__EFMigrationsHistory`) ensures each migration runs only once. The `dotnet ef database update` command is idempotent
5. **Schema-scoped**: Each service's `DbContext` specifies its schema:

```csharp
// In ContributionsDbContext
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("contributions");
    // ... entity configurations
}
```

### Rollback Strategy

- **Forward-only preferred**: Fix issues with a new forward migration rather than rolling back
- **Emergency rollback**: EF Core supports `dotnet ef database update <PreviousMigrationName>` which runs the `Down()` methods. This should only be used if the forward migration caused data corruption
- **Data migrations**: For data transformations (e.g., backfilling a new column), write a separate migration that uses raw SQL:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("""
        UPDATE contributions.contribution_dues 
        SET remaining_balance = amount_due - amount_paid 
        WHERE remaining_balance IS NULL;
    """);
}
```

### Deployment Order

Since all 7 schemas share one PostgreSQL instance, migrations must run sequentially to avoid lock conflicts:

```yaml
# In Docker Compose or deployment script
services:
  migrate:
    command: |
      dotnet ef database update --project Identity.Infrastructure
      dotnet ef database update --project FundAdmin.Infrastructure
      dotnet ef database update --project Contributions.Infrastructure
      dotnet ef database update --project Loans.Infrastructure
      dotnet ef database update --project Dissolution.Infrastructure
      dotnet ef database update --project Notifications.Infrastructure
      dotnet ef database update --project Audit.Infrastructure
```

---

## 5. Indexing Deep-Dive

### Decision

Targeted B-tree indexes on high-query-frequency columns, partial indexes for status filtering, GIN indexes for JSONB columns, and covering indexes for dashboard queries.

### Index Strategy Per Schema

#### Identity

```sql
-- User lookup by phone/email (OTP login)
CREATE UNIQUE INDEX ix_users_phone ON identity.users (phone) WHERE phone IS NOT NULL;
CREATE UNIQUE INDEX ix_users_email ON identity.users (email) WHERE email IS NOT NULL;

-- Active sessions by user
CREATE INDEX ix_sessions_user_active ON identity.sessions (user_id) 
    WHERE revoked = false AND expires_at > now();

-- OTP challenge lookup
CREATE INDEX ix_otp_user_channel ON identity.otp_challenges (user_id, channel, created_at DESC);
```

#### Fund Admin

```sql
-- Fund lookup by state (dashboard filtering)
CREATE INDEX ix_funds_state ON fundadmin.funds (state);

-- Role lookup: "what funds does this user belong to?"
CREATE INDEX ix_roles_user ON fundadmin.fund_role_assignments (user_id);

-- Role lookup: "who are the members of this fund?"
CREATE INDEX ix_roles_fund_role ON fundadmin.fund_role_assignments (fund_id, role);

-- Member plan: "get member's contribution amount for a fund"
CREATE INDEX ix_plans_user_fund ON fundadmin.member_contribution_plans (user_id, fund_id) 
    WHERE is_active = true;

-- Invitations: pending invitations for a fund
CREATE INDEX ix_invitations_fund_status ON fundadmin.invitations (fund_id, status) 
    WHERE status = 'Pending';
```

#### Contributions

```sql
-- Monthly contribution dashboard: "show all dues for fund X, month Y"
CREATE INDEX ix_dues_fund_month ON contributions.contribution_dues (fund_id, month_year);

-- Member's contribution history: "show all dues for user X in fund Y"
CREATE INDEX ix_dues_user_fund ON contributions.contribution_dues (user_id, fund_id, month_year DESC);

-- Overdue detection job: "find all Pending/Partial dues past due date"
CREATE INDEX ix_dues_status_duedate ON contributions.contribution_dues (status, due_date) 
    WHERE status IN ('Pending', 'Partial');

-- Ledger: fund transactions sorted by date
CREATE INDEX ix_transactions_fund_date ON contributions.transactions (fund_id, created_at DESC);

-- Idempotency dedup (already UNIQUE constraint, but explicit for clarity)
-- The UNIQUE constraint on (fund_id, idempotency_key) creates this implicitly
```

#### Loans

```sql
-- Fund's active loans
CREATE INDEX ix_loans_fund_status ON loans.loans (fund_id, status);

-- Borrower's loans
CREATE INDEX ix_loans_borrower ON loans.loans (borrower_id, fund_id);

-- Pending approvals queue (Fund Admin dashboard)
CREATE INDEX ix_loans_pending ON loans.loans (fund_id, created_at DESC) 
    WHERE status = 'PendingApproval';

-- Repayment entries for a loan (monthly schedule view)
CREATE INDEX ix_repayments_loan_month ON loans.repayment_entries (loan_id, month_year);

-- Overdue repayments detection
CREATE INDEX ix_repayments_overdue ON loans.repayment_entries (fund_id, status, due_date) 
    WHERE status IN ('Pending', 'Partial');

-- Monthly repayment generation: "find all active loans in a fund"
CREATE INDEX ix_loans_active ON loans.loans (fund_id) 
    WHERE status = 'Active';

-- Voting session lookup by loan
-- Already covered by UNIQUE constraint on (loan_id)
```

#### Notifications

```sql
-- Pending notifications to send (background worker)
CREATE INDEX ix_notifications_pending ON notifications.notifications (status, scheduled_at) 
    WHERE status = 'Pending';

-- Retry queue
CREATE INDEX ix_notifications_retry ON notifications.notifications (status, next_retry_at) 
    WHERE status = 'Pending' AND retry_count < max_retries;

-- User's notification feed (in-app)
CREATE INDEX ix_notifications_user ON notifications.notifications (recipient_id, created_at DESC);

-- User's notification feed for a specific fund
CREATE INDEX ix_notifications_user_fund ON notifications.notifications (recipient_id, fund_id, created_at DESC) 
    WHERE fund_id IS NOT NULL;
```

#### Audit

```sql
-- Entity history: "show all changes to this loan"
CREATE INDEX ix_audit_entity ON audit.audit_logs (entity_type, entity_id, timestamp DESC);

-- Actor history: "show all actions by this user"
CREATE INDEX ix_audit_actor ON audit.audit_logs (actor_id, timestamp DESC);

-- Fund-scoped audit trail
CREATE INDEX ix_audit_fund ON audit.audit_logs (fund_id, timestamp DESC) 
    WHERE fund_id IS NOT NULL;

-- GIN index for JSONB searching (admin debug queries)
CREATE INDEX ix_audit_after_state ON audit.audit_logs USING GIN (after_state jsonb_path_ops);
```

### Index Performance Notes

- **Partial indexes** (`WHERE status = 'Pending'`) are used extensively. They're much smaller than full-table indexes because they only include matching rows. For example, `ix_loans_pending` only indexes the small fraction of loans in PendingApproval status
- **Covering indexes**: For the contribution dashboard query (fund + month), all needed columns (user_id, amount_due, status) could be added as `INCLUDE` columns to enable index-only scans. However, at MVP scale this optimisation is premature — defer to post-MVP if query performance profiling reveals sequential scans
- **No index on `created_at` alone**: Nearly every `created_at` query also filters by `fund_id` or `user_id`, so composite indexes serve better than single-column indexes on timestamps

---

## 6. Connection Pooling

### Decision

Use **Npgsql built-in connection pooling** for MVP with pool sizes tuned per service. Add **PgBouncer** post-MVP if connection count becomes a bottleneck.

### Pool Sizing

7 microservices sharing one PostgreSQL 16 instance. PostgreSQL default `max_connections = 100`.

| Service | Expected Load | Pool Size (min/max) | Rationale |
|---------|--------------|---------------------|-----------|
| Identity | Low (login only) | 2 / 10 | Infrequent, short-lived queries |
| FundAdmin | Low-Medium | 2 / 15 | Fund creation, membership — not high frequency |
| Contributions | **High** (monthly batch) | 5 / 30 | Monthly cycle generates 1,000 dues per fund in batch |
| Loans | Medium-High | 3 / 25 | Repayment generation + payment recording |
| Dissolution | Low (rare) | 2 / 10 | Only during dissolution events |
| Notifications | Medium (burst) | 3 / 20 | Burst during monthly cycle; otherwise low |
| Audit | Medium (stream) | 3 / 15 | Constant stream of audit events from all services |
| **Total** | | **20 / 125** | |

**PostgreSQL configuration**:
```ini
# postgresql.conf
max_connections = 150   # 125 service connections + 25 buffer for admin/monitoring
```

**Npgsql connection string** (per service):
```
Host=localhost;Port=5432;Database=fundmanager;Username=svc_contributions;Password=...;
Search Path=contributions;
Minimum Pool Size=5;
Maximum Pool Size=30;
Connection Idle Lifetime=300;
Connection Pruning Interval=10;
```

### Why Npgsql Built-In for MVP

| Approach | Pros | Cons |
|----------|------|------|
| **Npgsql built-in** | Zero additional infrastructure; built into the driver; well-tested; supports connection lifetime and idle pruning | Each service maintains its own pool; total connections = sum of all pools |
| **PgBouncer** | Multiplexes connections across services; reduces total PostgreSQL connections; transaction-mode pooling | Additional container to deploy, configure, monitor; adds network hop latency; transaction-mode has limitations (prepared statements, LISTEN/NOTIFY) |

**Decision**: Npgsql built-in for MVP. The total pool ceiling (125 connections) is well within PostgreSQL 16's capability with moderate hardware (4 vCPU / 8 GB RAM).

**Trigger for PgBouncer**: If monitoring shows `pg_stat_activity` consistently >100 active connections, or connection wait times exceed 100ms, introduce PgBouncer in transaction-pooling mode.

### Per-Service Database Users

Each service connects with its own PostgreSQL role, which has access **only to its own schema**:

```sql
-- Create service roles
CREATE ROLE svc_identity LOGIN PASSWORD '...';
CREATE ROLE svc_fundadmin LOGIN PASSWORD '...';
CREATE ROLE svc_contributions LOGIN PASSWORD '...';
CREATE ROLE svc_loans LOGIN PASSWORD '...';
CREATE ROLE svc_dissolution LOGIN PASSWORD '...';
CREATE ROLE svc_notifications LOGIN PASSWORD '...';
CREATE ROLE svc_audit LOGIN PASSWORD '...';

-- Grant schema access
GRANT USAGE ON SCHEMA identity TO svc_identity;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA identity TO svc_identity;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity GRANT ALL ON TABLES TO svc_identity;

-- Repeat for each service/schema pair...

-- Audit service: append-only (no UPDATE/DELETE)
GRANT USAGE ON SCHEMA audit TO svc_audit;
GRANT INSERT, SELECT ON ALL TABLES IN SCHEMA audit TO svc_audit;
ALTER DEFAULT PRIVILEGES IN SCHEMA audit GRANT INSERT, SELECT ON TABLES TO svc_audit;
-- Explicitly NO UPDATE, NO DELETE privileges for audit schema
```

---

## 7. Table Partitioning

### Decision

Use **PostgreSQL declarative range partitioning** on high-volume, time-series tables: `transactions`, `audit_logs`, and `notifications`. Other tables remain unpartitioned.

### Partitioning Targets

| Table | Volume Estimate (yearly) | Partition Strategy |
|-------|------------------------|--------------------|
| `contributions.transactions` | 100 funds × 1,000 members × 12 months × ~2 txns = ~2.4M rows/year | Range by `month_year` or `created_at` (quarterly) |
| `audit.audit_logs` | ~10M rows/year (every state change across all services) | Range by `timestamp` (monthly) |
| `notifications.notifications` | ~5M rows/year | Range by `created_at` (quarterly) |
| `contributions.contribution_dues` | ~1.2M rows/year | **Not partitioned** — frequently queried by user+fund, not by date range |
| `loans.repayment_entries` | ~500K rows/year | **Not partitioned** — typically queried by loan_id |

### Implementation: Audit Logs (Monthly Partitions)

```sql
-- Create partitioned table
CREATE TABLE audit.audit_logs (
    id              uuid NOT NULL DEFAULT gen_random_uuid(),
    actor_id        uuid NOT NULL,
    fund_id         uuid,
    timestamp       timestamptz NOT NULL DEFAULT now(),
    action_type     varchar(50) NOT NULL,
    entity_type     varchar(50) NOT NULL,
    entity_id       uuid NOT NULL,
    before_state    jsonb,
    after_state     jsonb NOT NULL,
    ip_address      inet,
    user_agent      text,
    correlation_id  uuid,
    service_name    varchar(50) NOT NULL,
    
    PRIMARY KEY (id, timestamp)  -- partition key must be in PK
) PARTITION BY RANGE (timestamp);

-- Create initial partitions (automated via cron/script)
CREATE TABLE audit.audit_logs_2026_01 PARTITION OF audit.audit_logs
    FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE audit.audit_logs_2026_02 PARTITION OF audit.audit_logs
    FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
-- ... create partitions ahead of time (12 months forward)

-- Default partition for safety (catches data outside defined ranges)
CREATE TABLE audit.audit_logs_default PARTITION OF audit.audit_logs DEFAULT;
```

### Implementation: Transactions (Quarterly Partitions)

```sql
CREATE TABLE contributions.transactions (
    id                  uuid NOT NULL DEFAULT gen_random_uuid(),
    fund_id             uuid NOT NULL,
    user_id             uuid NOT NULL,
    type                varchar(30) NOT NULL,
    amount              numeric(18,2) NOT NULL,
    idempotency_key     varchar(64) NOT NULL,
    reference_entity_type varchar(30),
    reference_entity_id uuid,
    recorded_by         uuid NOT NULL,
    description         text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

CREATE TABLE contributions.transactions_2026_q1 PARTITION OF contributions.transactions
    FOR VALUES FROM ('2026-01-01') TO ('2026-04-01');
-- ...
```

### Partition Maintenance

```sql
-- Automated partition creation script (run monthly via cron)
-- Creates partitions 3 months ahead

DO $$
DECLARE
    partition_date date;
    partition_name text;
    start_date date;
    end_date date;
BEGIN
    FOR i IN 1..3 LOOP
        partition_date := date_trunc('month', now()) + (i || ' months')::interval;
        partition_name := 'audit_logs_' || to_char(partition_date, 'YYYY_MM');
        start_date := partition_date;
        end_date := partition_date + '1 month'::interval;
        
        IF NOT EXISTS (SELECT 1 FROM pg_tables WHERE tablename = partition_name AND schemaname = 'audit') THEN
            EXECUTE format(
                'CREATE TABLE audit.%I PARTITION OF audit.audit_logs FOR VALUES FROM (%L) TO (%L)',
                partition_name, start_date, end_date
            );
        END IF;
    END LOOP;
END $$;
```

### EF Core with Partitioned Tables

EF Core is unaware of partitioning — it queries the parent table, and PostgreSQL routes to the correct partition transparently. The main consideration:

```csharp
// Always include the partition key in queries for partition pruning
var recentAuditLogs = await _db.AuditLogs
    .Where(a => a.Timestamp >= startDate && a.Timestamp < endDate) // partition pruning
    .Where(a => a.EntityType == "Loan" && a.EntityId == loanId)
    .OrderByDescending(a => a.Timestamp)
    .ToListAsync();
```

Without the `Timestamp` filter, PostgreSQL scans all partitions — negating the benefit.

---

## 8. Backup, Retention & Archival

### Decision

**pg_dump** daily backups for MVP; WAL archiving for point-in-time recovery (PITR). 7-year data retention per spec requirement. Archival of old partitions to cold storage after 2 years.

### Backup Strategy

| Backup Type | Frequency | Tool | Retention |
|-------------|-----------|------|-----------|
| **Full logical backup** | Daily (02:00 IST) | `pg_dump --format=custom` | 30 days rolling |
| **WAL archiving** | Continuous | PostgreSQL `archive_command` or pgBackRest | 7 days of WAL files |
| **Point-in-Time Recovery** | On-demand | `pg_restore` + WAL replay | Up to 7 days back |

### Daily Backup Script

```bash
#!/bin/bash
# /opt/scripts/backup-fundmanager.sh
# Run via cron: 0 2 * * * /opt/scripts/backup-fundmanager.sh

BACKUP_DIR="/backups/fundmanager"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
FILENAME="fundmanager_${TIMESTAMP}.dump"

pg_dump \
  --host=localhost \
  --port=5432 \
  --username=backup_user \
  --dbname=fundmanager \
  --format=custom \
  --compress=9 \
  --file="${BACKUP_DIR}/${FILENAME}"

# Verify backup integrity
pg_restore --list "${BACKUP_DIR}/${FILENAME}" > /dev/null 2>&1
if [ $? -ne 0 ]; then
  echo "BACKUP VERIFICATION FAILED: ${FILENAME}" | mail -s "Backup Alert" admin@fundmanager.com
  exit 1
fi

# Remove backups older than 30 days
find "${BACKUP_DIR}" -name "*.dump" -mtime +30 -delete

echo "Backup completed: ${FILENAME}"
```

### 7-Year Data Retention Strategy

The spec requires: "Fund data retained for 7 years after dissolution per Indian financial record-keeping norms."

**Approach**: Three tiers of data lifecycle:

| Tier | Data Age | Storage | Access Pattern |
|------|----------|---------|----------------|
| **Hot** (0–2 years) | Active funds + recently dissolved | Primary PostgreSQL | Full read/write |
| **Warm** (2–5 years) | Dissolved funds older than 2 years | Archived partitions (detached) or read-replica | Read-only, on-demand |
| **Cold** (5–7 years) | Near end of retention | Compressed pg_dump exports in cloud storage (S3/Azure Blob) | Restore-on-request |

**Hot → Warm transition** (for partitioned tables):
```sql
-- Detach old partition (data remains but is no longer queried by default)
ALTER TABLE audit.audit_logs DETACH PARTITION audit.audit_logs_2024_01;

-- Optionally move to a separate "archive" tablespace on cheaper storage
ALTER TABLE audit.audit_logs_2024_01 SET TABLESPACE archive_storage;
```

**Warm → Cold transition**:
```bash
# Export old partition to compressed file
pg_dump --table=audit.audit_logs_2024_01 --format=custom --compress=9 \
  --file=/cold-storage/audit_logs_2024_01.dump

# Upload to cloud storage
aws s3 cp /cold-storage/audit_logs_2024_01.dump s3://fundmanager-archive/

# Drop the table after successful upload + verification
DROP TABLE audit.audit_logs_2024_01;
```

**Cold data restoration** (for regulatory/legal requests):
```bash
# Restore a specific partition from archive
aws s3 cp s3://fundmanager-archive/audit_logs_2024_01.dump /tmp/
pg_restore --dbname=fundmanager /tmp/audit_logs_2024_01.dump

# Re-attach as partition (or query as standalone table)
ALTER TABLE audit.audit_logs ATTACH PARTITION audit.audit_logs_2024_01
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
```

### Dissolved Fund Data

For **non-partitioned tables** (e.g., `loans.loans`, `contributions.contribution_dues`), dissolved fund data is retained in-place for 7 years. A background job marks dissolved funds for archival:

```sql
-- Identify funds eligible for archival (dissolved > 7 years ago)
SELECT id, name, updated_at FROM fundadmin.funds
WHERE state = 'Dissolved' AND updated_at < now() - interval '7 years';
```

At MVP scale (100 funds), even 7 years of data is manageable without archival. This strategy is designed for future scale.

---

## 9. PostgreSQL Extensions

### Decision

Minimal extension set for MVP. Only install extensions that solve a concrete requirement.

| Extension | Purpose | Required For |
|-----------|---------|-------------|
| **pgcrypto** | `gen_random_uuid()` for UUID generation | Primary key defaults (built-in since PG 13, but pgcrypto provides `crypt()` for OTP hashing) |
| **pg_trgm** | Trigram-based fuzzy text search | Member search by name (Fund Admin searching for members to invite). Enables `LIKE '%pattern%'` index support |
| **pg_stat_statements** | Query performance monitoring | Identifying slow queries; essential for performance tuning |
| ~~pg_uuidv7~~ | Time-ordered UUIDs | **Deferred to post-MVP** — UUIDv4 is sufficient; UUIDv7 improves B-tree locality but adds a runtime dependency |
| ~~PostGIS~~ | Geospatial | **Not needed** — no location features |
| ~~TimescaleDB~~ | Time-series | **Not needed** — PostgreSQL native partitioning is sufficient for audit logs |

### Installation

```sql
-- Run once during initial PostgreSQL setup
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
```

### pg_trgm Usage for Member Search

```sql
-- Create trigram index for member name search
CREATE INDEX ix_users_name_trgm ON identity.users USING GIN (name gin_trgm_ops);

-- Example fuzzy search query
SELECT id, name, phone, email 
FROM identity.users 
WHERE name % 'Ganesh'  -- trigram similarity match
ORDER BY similarity(name, 'Ganesh') DESC
LIMIT 20;
```

---

## 10. Performance Tuning

### Decision

Tune PostgreSQL configuration for the expected workload: mixed OLTP (payments, contributions) with some analytical queries (reports, dissolution calculations).

### Hardware Assumptions (MVP)

- **CPU**: 4 vCPU
- **RAM**: 8 GB (dedicated to PostgreSQL)
- **Storage**: SSD (NVMe preferred)
- **OS**: Linux (Ubuntu 22.04 / Debian 12)

### Key Configuration Parameters

```ini
# postgresql.conf — tuned for 8 GB RAM, 4 vCPU, SSD storage

# Memory
shared_buffers = 2GB                    # 25% of RAM
effective_cache_size = 6GB              # 75% of RAM — OS page cache + shared_buffers
work_mem = 16MB                         # Per-operation sort memory; increased from default 4MB for report queries
maintenance_work_mem = 512MB            # For VACUUM, CREATE INDEX, ALTER TABLE
wal_buffers = 64MB                      # Auto-tuned, but explicit for clarity

# Write-Ahead Log (WAL)
wal_level = replica                     # Required for PITR; enables WAL archiving
max_wal_size = 2GB                      # Before automatic checkpoint
min_wal_size = 512MB                    # Minimum WAL retention
checkpoint_completion_target = 0.9      # Spread I/O over 90% of checkpoint interval
archive_mode = on                       # Enable WAL archiving for PITR
archive_command = 'cp %p /wal_archive/%f' # Replace with pgBackRest in production

# Connections
max_connections = 150                   # 125 service pools + 25 admin/monitoring
superuser_reserved_connections = 3      # Reserve 3 for DBA access

# Query Planning
random_page_cost = 1.1                  # SSD: nearly sequential (down from default 4.0)
effective_io_concurrency = 200          # SSD: high concurrent I/O capability
default_statistics_target = 200         # More accurate query plans; increased from 100

# Autovacuum (critical for UPDATE-heavy tables like contribution_dues, repayment_entries)
autovacuum_max_workers = 4              # Parallel vacuum workers
autovacuum_naptime = 30s                # Check every 30 seconds (default: 60s)
autovacuum_vacuum_threshold = 50        # Vacuum after 50 dead tuples (default: 50)
autovacuum_vacuum_scale_factor = 0.05   # Vacuum when 5% of table is dead (default: 0.2)
autovacuum_analyze_threshold = 50       # Analyze after 50 changes (default: 50)
autovacuum_analyze_scale_factor = 0.05  # Analyze when 5% changed (default: 0.1)

# Logging
log_min_duration_statement = 200        # Log queries slower than 200ms
log_statement = 'ddl'                   # Log all DDL statements
log_checkpoints = on
log_connections = on
log_disconnections = on
log_lock_waits = on
log_temp_files = 0                      # Log all temp files (indicates insufficient work_mem)

# Statistics
track_activities = on
track_counts = on
track_io_timing = on                    # Required for pg_stat_statements I/O tracking
track_wal_io_timing = on
```

### Autovacuum Tuning Rationale

The `contribution_dues` and `repayment_entries` tables receive frequent UPDATEs (status transitions, payment recording). PostgreSQL's MVCC creates dead tuples on each UPDATE. Aggressive autovacuum settings ensure:

1. **Dead tuples are reclaimed quickly** → prevents table bloat
2. **Statistics stay current** → accurate query plans
3. **Transaction ID wraparound prevention** → critical for long-running systems

For the append-only `transactions` and `audit_logs` tables, autovacuum is less critical (no dead tuples from UPDATEs), but analyze is still needed for accurate statistics after bulk inserts (monthly contribution cycle).

### Per-Table Autovacuum Overrides

```sql
-- Contribution dues: heavily updated, needs aggressive vacuum
ALTER TABLE contributions.contribution_dues SET (
    autovacuum_vacuum_scale_factor = 0.02,  -- vacuum at 2% dead
    autovacuum_analyze_scale_factor = 0.02
);

-- Transactions: append-only, minimal vacuum needed
ALTER TABLE contributions.transactions SET (
    autovacuum_vacuum_scale_factor = 0.1,   -- less frequent
    autovacuum_enabled = true                -- still needed for analyze
);

-- Audit logs: append-only
ALTER TABLE audit.audit_logs SET (
    autovacuum_vacuum_scale_factor = 0.1
);
```

---

## 11. Encryption at Rest

### Decision

Use **volume-level encryption** (LUKS on Linux, or cloud-provided disk encryption) for MVP. PostgreSQL itself does not natively support encryption at rest in the community edition.

### Options Evaluated

| Approach | Encryption Level | Complexity | Performance Impact |
|----------|-----------------|------------|-------------------|
| **Volume-level (LUKS / cloud disk)** | Full disk | Low (transparent) | < 2% (hardware AES-NI) |
| **PostgreSQL TDE (enterprise)** | Tablespace | Medium | ~5% | 
| **pgcrypto per-column** | Column value | High (application changes) | 10–30% (per-query decrypt) |

**Decision**: Volume-level encryption. It satisfies the spec requirement (FR-113: "All data at rest MUST be encrypted") with zero application changes and negligible performance impact.

**Docker Compose (local dev)**: Not encrypted — acceptable for development. The volume encryption is a deployment-environment concern.

**Production**: Cloud provider's disk encryption (e.g., Azure Disk Encryption with platform-managed keys, AWS EBS encryption). Or LUKS for self-hosted deployments.

### Sensitive Column Encryption (Defence in Depth)

For highly sensitive fields (OTP hashes, session tokens), additional column-level protection:

```sql
-- OTP stored as bcrypt hash (via pgcrypto or application-level hashing)
-- NEVER store plaintext OTPs
INSERT INTO identity.otp_challenges (otp_hash)
VALUES (crypt('123456', gen_salt('bf', 10)));

-- Verify OTP
SELECT id FROM identity.otp_challenges
WHERE otp_hash = crypt('123456', otp_hash)
  AND expires_at > now()
  AND verified = false;
```

**Note**: Phone numbers and email addresses are **not encrypted at the column level** for MVP — they are needed for querying (login, search, notifications). If PII regulations tighten, consider `pgcrypto` column encryption or a dedicated secrets vault.

---

## 12. Idempotency Table Design

### Decision

Each service that handles payment/mutation operations maintains an `idempotency_records` table within its schema. This is separate from the Transaction table's unique constraint — it captures the full response for replay.

> **Note**: The `contributions.transactions` table already has `UNIQUE (fund_id, idempotency_key)` which prevents duplicate financial entries. The `idempotency_records` table below serves a complementary purpose: caching the full API response so duplicate requests can be replayed without re-executing business logic.

### DDL

```sql
-- In each service schema that handles payment mutations
CREATE TABLE contributions.idempotency_records (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    idempotency_key varchar(64) NOT NULL,
    fund_id         uuid NOT NULL,
    endpoint        varchar(100) NOT NULL,    -- e.g., 'POST /contributions/payments'
    request_hash    varchar(64) NOT NULL,     -- SHA-256 of request body (detect mismatched replays)
    status_code     integer NOT NULL,          -- HTTP status code of original response
    response_body   jsonb NOT NULL,            -- serialised original response
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL,      -- auto-cleanup after 90 days

    CONSTRAINT uq_idempotency_record UNIQUE (fund_id, idempotency_key, endpoint)
);

-- Index for cleanup job
CREATE INDEX ix_idempotency_expires ON contributions.idempotency_records (expires_at) 
    WHERE expires_at < now();
```

### How It Works

```text
1. Client sends POST /api/contributions/payments with header: Idempotency-Key: abc-123
2. Middleware checks idempotency_records for (fund_id, "abc-123", endpoint)
3. If found → return cached response_body with cached status_code (no business logic)
4. If NOT found → execute business logic → store result in idempotency_records → return response
5. Background job: DELETE FROM idempotency_records WHERE expires_at < now() (daily)
```

### Request Hash Validation

If a client sends the same `Idempotency-Key` but a **different** request body (e.g., different amount), the system returns `422 Unprocessable Entity`:

```json
{
  "error": "IDEMPOTENCY_KEY_REUSE",
  "message": "This idempotency key was already used with a different request payload. Use a new key."
}
```

This prevents accidental misuse of idempotency keys.

### Retention

- **90-day retention** for idempotency records (matches financial dispute resolution window)
- **Cleanup job**: Runs daily, deletes expired records:

```sql
DELETE FROM contributions.idempotency_records 
WHERE expires_at < now();
```

---

## 13. Audit Table Design

### Decision

The audit table is **append-only** (no UPDATE or DELETE allowed), stores before/after state as **JSONB**, and is **partitioned monthly** for performance and archival.

### Design Principles

1. **Immutability**: Database privileges prevent UPDATE/DELETE. Application code only INSERTs
2. **Self-contained**: Each audit entry contains the complete before/after state — no need to join with the source table to reconstruct history
3. **Cross-service**: All 7 services emit audit events via MassTransit; the Audit service consumes and persists them
4. **Queryable**: `entity_type + entity_id` indexed for point lookups; JSONB GIN indexed for field-level searches
5. **Partitioned**: Monthly partitions for performance and 7-year retention management

### JSONB Before/After State

```json
// Example: Loan approval audit entry
{
  "id": "a1b2c3d4-...",
  "actor_id": "user-uuid-...",
  "fund_id": "fund-uuid-...",
  "timestamp": "2026-02-20T10:30:00Z",
  "action_type": "Loan.Approved",
  "entity_type": "Loan",
  "entity_id": "loan-uuid-...",
  "before_state": {
    "status": "PendingApproval",
    "approved_by": null,
    "scheduled_installment": 0.00
  },
  "after_state": {
    "status": "Approved",
    "approved_by": "admin-user-uuid-...",
    "scheduled_installment": 2000.00,
    "approval_date": "2026-02-20T10:30:00Z"
  },
  "service_name": "FundManager.Loans",
  "correlation_id": "req-uuid-..."
}
```

### Privilege Enforcement

```sql
-- The svc_audit role can only INSERT and SELECT
GRANT USAGE ON SCHEMA audit TO svc_audit;
GRANT INSERT, SELECT ON ALL TABLES IN SCHEMA audit TO svc_audit;
ALTER DEFAULT PRIVILEGES IN SCHEMA audit GRANT INSERT, SELECT ON TABLES TO svc_audit;

-- Explicitly deny UPDATE and DELETE by NOT granting them
-- Additional safety: trigger that blocks UPDATE/DELETE
CREATE OR REPLACE FUNCTION audit.prevent_modification()
RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'Audit logs are immutable. UPDATE and DELETE operations are not permitted.';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tr_audit_immutable
    BEFORE UPDATE OR DELETE ON audit.audit_logs
    FOR EACH ROW
    EXECUTE FUNCTION audit.prevent_modification();
```

### Audit Query Patterns

```sql
-- Get full history of a specific loan
SELECT * FROM audit.audit_logs
WHERE entity_type = 'Loan' AND entity_id = 'loan-uuid-...'
  AND timestamp >= '2026-01-01' AND timestamp < '2026-03-01'  -- partition pruning
ORDER BY timestamp;

-- Find who approved loans in fund X last month
SELECT actor_id, entity_id, timestamp, after_state
FROM audit.audit_logs
WHERE fund_id = 'fund-uuid-...'
  AND action_type = 'Loan.Approved'
  AND timestamp >= '2026-01-01' AND timestamp < '2026-02-01'
ORDER BY timestamp DESC;

-- Search for a specific field value in after_state (GIN index)
SELECT * FROM audit.audit_logs
WHERE after_state @> '{"status": "Rejected"}'
  AND entity_type = 'Loan'
  AND timestamp >= '2026-02-01';
```

---

## 14. Summary of Decisions

| Area | Decision | Key Technology / Config |
|------|----------|------------------------|
| **Column types** | UUID PKs, `numeric(18,2)` for money, `numeric(8,6)` for rates, `varchar` for enums, `timestamptz` for timestamps, `jsonb` for structured data | Npgsql default mappings + explicit EF Core config |
| **Money type** | `numeric(18,2)` — NOT `money` type | Locale-independent, exact arithmetic |
| **UUID strategy** | UUIDv4 client-generated (`Guid.NewGuid()`); DB default `gen_random_uuid()` as fallback | Built-in PG 13+ function |
| **Schema design** | 7 schemas (identity, fundadmin, contributions, loans, dissolution, notifications, audit) per service in single PG instance | EF Core `HasDefaultSchema()` |
| **Enum storage** | `varchar` string + CHECK constraints | Forward-compatible, human-readable |
| **Migrations** | EF Core Code-First; named `YYYYMMDD_HHMMSS_Service_Description`; no destructive changes on financial tables | `dotnet ef migrations add` |
| **Indexing** | B-tree (PK/FK), partial indexes (status filters), GIN (JSONB audit), trigram (name search) | PostgreSQL native indexes |
| **Connection pooling** | Npgsql built-in; per-service pool sizing (125 total); PgBouncer deferred to post-MVP | Npgsql connection string params |
| **Partitioning** | Monthly (audit_logs), quarterly (transactions, notifications); declarative range partitioning | PostgreSQL 16 native partitioning |
| **Backup** | Daily pg_dump + continuous WAL archiving; 30-day backup retention; 7-year data retention via hot/warm/cold tiers | pg_dump, pgBackRest |
| **Extensions** | pgcrypto, pg_trgm, pg_stat_statements | Minimal set for MVP |
| **Performance tuning** | Aggressive autovacuum for UPDATE-heavy tables; tuned shared_buffers (2GB), work_mem (16MB), SSD-optimised random_page_cost (1.1) | postgresql.conf |
| **Encryption at rest** | Volume-level encryption (LUKS / cloud disk); column-level bcrypt for OTP hashes | AES-NI hardware acceleration |
| **Idempotency** | Per-service `idempotency_records` table; 90-day retention; request hash validation | Fund-scoped unique key |
| **Audit design** | Append-only; JSONB before/after state; monthly partitioned; no UPDATE/DELETE privileges; immutability trigger | Privilege-based + trigger-based enforcement |
| **Per-service DB users** | Separate PostgreSQL roles per service schema; audit role = INSERT+SELECT only | GRANT/REVOKE privileges |

---

## Open Questions for Design Phase

1. **Read replicas for reports**: Should report-heavy queries (dissolution calculations, fund balance sheets) run against a read replica to avoid impacting OLTP performance? At MVP scale probably not needed, but the architecture should support it.
2. **pg_cron for partition maintenance**: Use `pg_cron` extension for automated partition creation & idempotency cleanup, or external cron? `pg_cron` runs inside PostgreSQL and eliminates external dependency.
3. **Connection pool warm-up**: At service startup, should connections be pre-warmed (`Minimum Pool Size > 0`) or established on-demand? Pre-warming recommended for Contributions service (batch job at month start).
4. **JSONB vs separate columns for audit before/after**: JSONB is flexible but not type-safe. Should critical audit fields (e.g., `status`, `amount`) be extracted into dedicated columns for faster indexed queries, with JSONB for the full state?
5. **Full-text search**: Should the platform support full-text search across audit logs (PostgreSQL `tsvector`)? Deferred for now — GIN index on JSONB + trigram on names covers MVP needs.
