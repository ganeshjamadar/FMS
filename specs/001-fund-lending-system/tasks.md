# Tasks: Generic Multi-Fund Monthly Contribution & Reducing-Balance Lending System

**Input**: Design documents from `/specs/001-fund-lending-system/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Not explicitly requested in the feature specification. Test tasks are omitted. Unit/integration tests can be added later via a dedicated testing pass.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Backend services**: `src/services/FundManager.<Service>/Api|Domain|Infrastructure/`
- **Shared packages**: `src/shared/FundManager.<Package>/`
- **API Gateway**: `src/gateway/FundManager.ApiGateway/`
- **Web app**: `src/web/fund-manager-web/`
- **Mobile app**: `src/mobile/fund-manager-mobile/`
- **Infrastructure**: `infrastructure/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository scaffolding, solution structure, Docker Compose, and shared packages

- [X] T001 Create .NET solution file `FundManager.sln` at repository root with folder structure per plan.md Project Structure section
- [X] T002 [P] Create `src/shared/FundManager.ServiceDefaults/` project with OpenTelemetry, Serilog, health-check defaults, and `IServiceCollection` extensions per research.md Section 5
- [X] T003 [P] Create `src/shared/FundManager.BuildingBlocks/` project with `MoneyMath` utility (banker's rounding, decimal helpers), base Entity/AggregateRoot classes, `IUnitOfWork`, `Result<T>`, and `FundId` scoping middleware per research.md Sections 3 and 6
- [X] T004 [P] Create `src/shared/FundManager.Contracts/` project with shared integration event interfaces, base event record, and `IIdempotencyKey` per research.md Section 7
- [X] T005 [P] Create `infrastructure/docker-compose.yml` with PostgreSQL 16, RabbitMQ 3.13, and Redis 7 containers; create `infrastructure/init-db/01-schemas.sql` with 7 schema definitions (`identity`, `fundadmin`, `contributions`, `loans`, `dissolution`, `notifications`, `audit`) per research-database.md Section 2
- [X] T006 [P] Create `.env.sample` with all required environment variables per quickstart.md Section 1
- [X] T007 [P] Initialise web app project: `src/web/fund-manager-web/` with Vite + React 18 + TypeScript 5.x + Tailwind CSS + React Router + TanStack Query + Zustand per research-frontend.md
- [X] T008 [P] Initialise mobile app project: `src/mobile/fund-manager-mobile/` with Expo managed workflow + React Native 0.73+ + React Navigation + TanStack Query per research-frontend.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can start

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T009 Create `src/services/FundManager.Identity/` service with Api, Domain, and Infrastructure projects; configure EF Core DbContext with `identity` schema, connection string, and first migration per research-database.md Section 4
- [X] T010 [P] Create `src/services/FundManager.FundAdmin/` service with Api, Domain, and Infrastructure projects; configure EF Core DbContext with `fundadmin` schema per research-database.md
- [X] T011 [P] Create `src/services/FundManager.Contributions/` service with Api, Domain, and Infrastructure projects; configure EF Core DbContext with `contributions` schema per research-database.md
- [X] T012 [P] Create `src/services/FundManager.Loans/` service with Api, Domain, and Infrastructure projects; configure EF Core DbContext with `loans` schema per research-database.md
- [X] T013 [P] Create `src/services/FundManager.Dissolution/` service with Api, Domain, and Infrastructure projects; configure EF Core DbContext with `dissolution` schema per research-database.md
- [X] T014 [P] Create `src/services/FundManager.Notifications/` service with Api, Domain, and Infrastructure projects; configure EF Core DbContext with `notifications` schema per research-database.md
- [X] T015 [P] Create `src/services/FundManager.Audit/` service with Api, Domain, and Infrastructure projects; configure EF Core DbContext with `audit` schema per research-database.md
- [X] T016 Implement JWT token generation and validation in `src/shared/FundManager.BuildingBlocks/Auth/JwtTokenService.cs`; configure ASP.NET Core authentication middleware shared by all services per research.md Section 5
- [X] T017 [P] Implement `FundIdScopingMiddleware` in `src/shared/FundManager.BuildingBlocks/Middleware/FundIdScopingMiddleware.cs` to extract `fundId` from route/header and set on `HttpContext.Items` per research.md Section 3
- [X] T018 [P] Implement `IdempotencyMiddleware` in `src/shared/FundManager.BuildingBlocks/Middleware/IdempotencyMiddleware.cs` with `IdempotencyRecord` EF entity per research.md Section 7
- [X] T019 [P] Implement role-based authorization policies (`PlatformAdmin`, `FundAdmin`, `FundEditor`, `FundGuest`) in `src/shared/FundManager.BuildingBlocks/Auth/FundAuthorizationPolicies.cs` per spec.md Section 4 Role Matrix
- [X] T020 Configure MassTransit with RabbitMQ transport in `src/shared/FundManager.BuildingBlocks/Messaging/MassTransitConfig.cs`; add `IPublishEndpoint` DI registration pattern per research.md Section 2
- [X] T021 Implement audit event publishing base: `AuditEventPublisher` in `src/shared/FundManager.BuildingBlocks/Audit/AuditEventPublisher.cs` that publishes `AuditLogCreated` events via MassTransit per research.md Section 2
- [X] T022 Create `src/gateway/FundManager.ApiGateway/` project with YARP reverse proxy configuration routing `/api/identity/*`, `/api/fundadmin/*`, `/api/funds/{fundId}/contributions/*`, `/api/funds/{fundId}/loans/*`, `/api/funds/{fundId}/dissolution/*`, `/api/notifications/*`, `/api/funds/{fundId}/audit/*`, `/api/funds/{fundId}/reports/*` per research.md Section 5
- [X] T023 [P] Create shared API client layer in `src/web/fund-manager-web/src/services/apiClient.ts` with Axios instance, auth token interceptor, error handling, and base URL configuration per research-frontend.md Section 3
- [X] T024 [P] Create shared API client for mobile in `src/mobile/fund-manager-mobile/src/services/apiClient.ts` with offline queue wrapper per research-frontend.md Section 5
- [X] T025 [P] Implement `OfflineSyncManager` in `src/mobile/fund-manager-mobile/src/services/offlineSync.ts` with AsyncStorage queue and network-aware sync per research-frontend.md Section 5
- [X] T026 Implement EF Core `AuditLog` entity and `AuditLogConsumer` in `src/services/FundManager.Audit/Infrastructure/Consumers/AuditLogConsumer.cs` to persist audit events from MassTransit per data-model.md AuditLog entity and contracts/audit-api.yaml
- [X] T027 Add EF Core migration for `audit.audit_logs` table (append-only, monthly partitioned) with immutability trigger per research-database.md Sections 7, 12, and 13

**Checkpoint**: Foundation ready — all services scaffolded, auth/authz wired, messaging configured, audit pipeline active. User story implementation can now begin.

---

## Phase 3: User Story 1 — Platform Admin Creates & Assigns a Fund (Priority: P1) 🎯 MVP

**Goal**: Platform Admin can create a fund with full configuration, assign a Fund Admin, and activate the fund.

**Independent Test**: Create a fund via API, assign an admin, activate it, verify all config fields persist and state transitions work.

### Implementation

- [X] T028 [P] [US1] Create `User` domain entity in `src/services/FundManager.Identity/Domain/Entities/User.cs` with Id (UUID), Name, Phone, Email, ProfilePictureUrl, IsActive, CreatedAt per data-model.md
- [X] T029 [P] [US1] Create `OtpChallenge` domain entity in `src/services/FundManager.Identity/Domain/Entities/OtpChallenge.cs` with Id, UserId, Channel, Target, OtpHash, ExpiresAt, Attempts, Verified per data-model.md
- [X] T030 [P] [US1] Create `Session` domain entity in `src/services/FundManager.Identity/Domain/Entities/Session.cs` with Id, UserId, Token, ExpiresAt, RevokedAt per data-model.md
- [X] T031 [US1] Implement OTP authentication flow: `OtpService` in `src/services/FundManager.Identity/Domain/Services/OtpService.cs` with RequestOtp (generate + hash + persist + publish notification event) and VerifyOtp (validate + create session + issue JWT) per contracts/identity-api.yaml
- [X] T032 [US1] Implement Identity API endpoints in `src/services/FundManager.Identity/Api/Controllers/AuthController.cs`: POST `/auth/otp/request`, POST `/auth/otp/verify`, POST `/auth/logout` per contracts/identity-api.yaml
- [X] T033 [US1] Implement Profile API endpoints in `src/services/FundManager.Identity/Api/Controllers/ProfileController.cs`: GET/PUT `/profile`, GET `/profile/funds` per contracts/identity-api.yaml
- [X] T034 [US1] Add EF Core migration for `identity.users`, `identity.otp_challenges`, `identity.sessions` tables per data-model.md
- [X] T035 [P] [US1] Create `Fund` domain entity in `src/services/FundManager.FundAdmin/Domain/Entities/Fund.cs` with all configuration fields, state machine (Draft→Active→Dissolving→Dissolved), and xmin concurrency token per data-model.md Fund entity
- [X] T036 [P] [US1] Create `FundMember` domain entity in `src/services/FundManager.FundAdmin/Domain/Entities/FundMember.cs` with FundId, UserId, Role, MonthlyContributionAmount, JoinDate, IsActive per data-model.md
- [X] T037 [US1] Implement `FundService` in `src/services/FundManager.FundAdmin/Domain/Services/FundService.cs` with CreateFund (validate config, set Draft state), ActivateFund (Draft→Active, require at least 1 Admin), UpdateFund (description in any state; all config fields in Draft state) per FR-010, FR-011, FR-012, FR-013, FR-015
- [X] T038 [US1] Implement Fund Admin API endpoints in `src/services/FundManager.FundAdmin/Api/Controllers/FundsController.cs`: GET/POST `/funds`, GET/PATCH `/funds/{fundId}`, POST `/funds/{fundId}/activate`, GET `/funds/{fundId}/dashboard` per contracts/fundadmin-api.yaml
- [X] T039 [US1] Implement Member management endpoints in `src/services/FundManager.FundAdmin/Api/Controllers/MembersController.cs`: GET `/funds/{fundId}/members`, PUT `/funds/{fundId}/members/{userId}/role`, DELETE `/funds/{fundId}/members/{userId}` per contracts/fundadmin-api.yaml
- [X] T040 [US1] Add EF Core migration for `fundadmin.funds` and `fundadmin.fund_members` tables with indexes per data-model.md and research-database.md Section 5
- [X] T041 [US1] Implement FluentValidation validators: `CreateFundValidator`, `UpdateFundValidator` in `src/services/FundManager.FundAdmin/Api/Validators/` per data-model.md Validation Rules
- [X] T042 [US1] Publish `FundCreated`, `FundActivated`, `FundAdminAssigned` integration events via MassTransit from FundService per research.md Section 2
- [X] T043 [US1] Create web pages: Fund List page in `src/web/fund-manager-web/src/pages/funds/FundListPage.tsx` and Create Fund form in `src/web/fund-manager-web/src/pages/funds/CreateFundPage.tsx` per spec.md UI Screens
- [X] T044 [US1] Create web page: Fund Detail/Dashboard page in `src/web/fund-manager-web/src/pages/funds/FundDashboardPage.tsx` showing fund summary, member count, balance per contracts/fundadmin-api.yaml FundDashboard schema
- [X] T045 [US1] Create mobile screen: Fund List screen in `src/mobile/fund-manager-mobile/src/screens/funds/FundListScreen.tsx` per spec.md Mobile UI

**Checkpoint**: Fund creation, admin assignment, and activation work end-to-end through API, web, and mobile.

---

## Phase 4: User Story 2 — Fund Admin Manages Membership (Priority: P1)

**Goal**: Fund Admin can invite members, members accept with contribution amount, and admin can manage/remove members.

**Independent Test**: Invite a user by phone, have them accept with a contribution amount ≥ fund minimum, verify membership record created.

### Implementation

- [X] T046 [P] [US2] Create `Invitation` domain entity in `src/services/FundManager.FundAdmin/Domain/Entities/Invitation.cs` with Id, FundId, InvitedBy, TargetContact, Status (Pending→Accepted|Declined|Expired), ExpiresAt per data-model.md
- [X] T047 [US2] Implement `InvitationService` in `src/services/FundManager.FundAdmin/Domain/Services/InvitationService.cs` with InviteMember (create invitation + publish notification event), AcceptInvitation (validate contribution ≥ minimum, create FundMember), DeclineInvitation per FR-020, FR-021, FR-022, FR-023
- [X] T048 [US2] Implement `MemberService` in `src/services/FundManager.FundAdmin/Domain/Services/MemberService.cs` with RemoveMember (check no outstanding loans/unpaid dues via cross-service query), ChangeRole (prevent demoting last Admin) per FR-024, FR-015
- [X] T049 [US2] Implement Invitation API endpoints in `src/services/FundManager.FundAdmin/Api/Controllers/InvitationsController.cs`: GET/POST `/funds/{fundId}/invitations`, POST `/invitations/{invitationId}/accept`, POST `/invitations/{invitationId}/decline` per contracts/fundadmin-api.yaml
- [X] T050 [US2] Add EF Core migration for `fundadmin.invitations` table per data-model.md
- [X] T051 [US2] Publish `MemberJoined`, `MemberRemoved`, `InvitationSent` integration events via MassTransit per research.md Section 2
- [X] T052 [US2] Implement FluentValidation: `InviteMemberValidator`, `AcceptInvitationValidator` in `src/services/FundManager.FundAdmin/Api/Validators/` per data-model.md
- [X] T053 [P] [US2] Create web pages: Member List page in `src/web/fund-manager-web/src/pages/members/MemberListPage.tsx` and Invite Member modal in `src/web/fund-manager-web/src/pages/members/InviteMemberModal.tsx`
- [X] T054 [P] [US2] Create mobile screens: Member List in `src/mobile/fund-manager-mobile/src/screens/members/MemberListScreen.tsx` and Accept Invitation flow in `src/mobile/fund-manager-mobile/src/screens/invitations/AcceptInvitationScreen.tsx`

**Checkpoint**: Full invitation → acceptance → membership flow works. Members visible in fund dashboard.

---

## Phase 5: User Story 3 — Monthly Contribution Cycle (Priority: P1)

**Goal**: System generates monthly dues, members/admins record payments, ledger tracks all transactions.

**Independent Test**: Trigger monthly cycle for a fund with 5 members, verify 5 dues created, record a full payment, verify ledger entry.

### Implementation

- [X] T055 [P] [US3] Create `ContributionDue` domain entity in `src/services/FundManager.Contributions/Domain/Entities/ContributionDue.cs` with all fields, status enum (Pending/Paid/Partial/Late/Missed), xmin concurrency token per data-model.md
- [X] T056 [P] [US3] Create `Transaction` domain entity in `src/services/FundManager.Contributions/Domain/Entities/Transaction.cs` as append-only with Type enum, polymorphic reference (ReferenceEntityType/Id) per data-model.md
- [X] T057 [US3] Implement `ContributionCycleService` in `src/services/FundManager.Contributions/Domain/Services/ContributionCycleService.cs` with GenerateDues (idempotent, fetch active members from FundAdmin via event-carried state, create ContributionDue per member) per FR-030, FR-031, FR-032
- [X] T058 [US3] Implement `PaymentService` in `src/services/FundManager.Contributions/Domain/Services/PaymentService.cs` with RecordPayment (validate idempotency key, optimistic concurrency via If-Match, create Transaction, update ContributionDue status, handle partial/full) per FR-035, FR-035a, FR-037
- [X] T059 [US3] Implement `OverdueDetectionService` in `src/services/FundManager.Contributions/Domain/Services/OverdueDetectionService.cs` with MarkLate (Pending→Late after grace period) and MarkMissed (Late→Missed at month-end) per FR-033, FR-034
- [X] T060 [US3] Implement Contributions API endpoints in `src/services/FundManager.Contributions/Api/Controllers/DuesController.cs`: GET `/dues`, GET `/dues/{dueId}`, POST `/dues/generate`, GET `/summary` per contracts/contributions-api.yaml
- [X] T061 [US3] Implement Payments API endpoint in `src/services/FundManager.Contributions/Api/Controllers/PaymentsController.cs`: POST `/payments` with Idempotency-Key and If-Match headers per contracts/contributions-api.yaml
- [X] T062 [US3] Implement Ledger API endpoint in `src/services/FundManager.Contributions/Api/Controllers/LedgerController.cs`: GET `/ledger` with filters per contracts/contributions-api.yaml
- [X] T063 [US3] Add EF Core migration for `contributions.contribution_dues`, `contributions.transactions` tables with indexes and quarterly partitioning for transactions per data-model.md and research-database.md Sections 5, 7
- [X] T064 [US3] Implement `MemberJoinedConsumer` in `src/services/FundManager.Contributions/Infrastructure/Consumers/MemberJoinedConsumer.cs` to maintain local member cache/projection for due generation per research.md Section 2
- [X] T065 [US3] Publish `ContributionPaid`, `ContributionOverdue`, `ContributionDueGenerated` integration events per research.md Section 2
- [X] T066 [P] [US3] Create web pages: Contribution dues list in `src/web/fund-manager-web/src/pages/contributions/ContributionDuesPage.tsx` and Record Payment form in `src/web/fund-manager-web/src/pages/contributions/RecordPaymentModal.tsx`
- [X] T067 [P] [US3] Create web page: Fund Ledger page in `src/web/fund-manager-web/src/pages/ledger/LedgerPage.tsx` with transaction table and filters
- [X] T068 [P] [US3] Create mobile screens: My Dues screen in `src/mobile/fund-manager-mobile/src/screens/contributions/MyDuesScreen.tsx` and Record Payment in `src/mobile/fund-manager-mobile/src/screens/contributions/RecordPaymentScreen.tsx`

**Checkpoint**: Monthly contribution cycle generates dues, payments are recorded with idempotency+concurrency, ledger shows all transactions.

---

## Phase 6: User Story 4 — Member Requests a Loan (Priority: P1)

**Goal**: Members submit loan requests, Fund Admin approves/rejects, disbursement creates a transaction.

**Independent Test**: Submit a loan request, have admin approve with scheduled installment, verify disbursement transaction and pool balance reduction.

### Implementation

- [X] T069 [P] [US4] Create `Loan` domain entity in `src/services/FundManager.Loans/Domain/Entities/Loan.cs` with all fields, status enum (PendingApproval/Approved/Active/Closed/Rejected), snapshot fields (MonthlyInterestRate, MinimumPrincipal, ScheduledInstallment), xmin concurrency token per data-model.md
- [X] T070 [US4] Implement `LoanRequestService` in `src/services/FundManager.Loans/Domain/Services/LoanRequestService.cs` with RequestLoan (validate against pool balance, max loan cap, max concurrent loans), ApproveLoan (set ScheduledInstallment, publish disbursement event, PendingApproval→Active), RejectLoan (with reason) per FR-040 through FR-051
- [X] T071 [US4] Implement Loans API endpoints in `src/services/FundManager.Loans/Api/Controllers/LoansController.cs`: GET `/`, POST `/`, GET `/{loanId}`, POST `/{loanId}/approve`, POST `/{loanId}/reject` per contracts/loans-api.yaml
- [X] T072 [US4] Add EF Core migration for `loans.loans` table with indexes per data-model.md
- [X] T073 [US4] Implement `LoanDisbursedConsumer` in `src/services/FundManager.Contributions/Infrastructure/Consumers/LoanDisbursedConsumer.cs` to create disbursement Transaction in fund ledger when loan is approved per research.md Section 2
- [X] T074 [US4] Publish `LoanRequested`, `LoanApproved`, `LoanRejected`, `LoanDisbursed` integration events per research.md Section 2
- [X] T075 [US4] Implement FluentValidation: `LoanRequestValidator`, `ApproveLoanValidator` in `src/services/FundManager.Loans/Api/Validators/` per data-model.md Validation Rules
- [X] T076 [P] [US4] Create web pages: Loan Approval Queue in `src/web/fund-manager-web/src/pages/loans/LoanApprovalPage.tsx` and Loan Detail page in `src/web/fund-manager-web/src/pages/loans/LoanDetailPage.tsx`
- [X] T077 [P] [US4] Create mobile screens: Request Loan form in `src/mobile/fund-manager-mobile/src/screens/loans/RequestLoanScreen.tsx` and My Loans list in `src/mobile/fund-manager-mobile/src/screens/loans/MyLoansScreen.tsx`

**Checkpoint**: Loan request → validation → approval → disbursement flow works. Fund pool balance decremented.

---

## Phase 7: User Story 6 — Loan Repayment on Reducing Balance (Priority: P1)

**Goal**: System generates monthly repayment entries using reducing-balance formula, members/admins record repayments.

**Independent Test**: With a loan of INR 50,000 at 2% monthly, verify first month: interest=1000, principal≥1000. Record payment, verify outstanding principal reduced.

### Implementation

- [X] T078 [P] [US6] Create `RepaymentEntry` domain entity in `src/services/FundManager.Loans/Domain/Entities/RepaymentEntry.cs` with InterestDue, PrincipalDue, TotalDue, AmountPaid, Status, xmin concurrency token per data-model.md
- [X] T079 [US6] Implement `RepaymentCalculationService` in `src/services/FundManager.Loans/Domain/Services/RepaymentCalculationService.cs` with CalculateMonthlyRepayment (interest_due = outstanding × rate; principal_due = max(minimumPrincipal, scheduledInstallment) capped at remaining) using `MoneyMath` for banker's rounding per FR-060 through FR-064, spec.md Section 6.4
- [X] T080 [US6] Implement `RepaymentRecordingService` in `src/services/FundManager.Loans/Domain/Services/RepaymentRecordingService.cs` with RecordRepayment (validate idempotency, optimistic concurrency, apply interest first then principal per FR-068, handle excess, check if loan fully repaid → Close) per FR-067, FR-068
- [X] T081 [US6] Implement Repayment API endpoints in `src/services/FundManager.Loans/Api/Controllers/RepaymentsController.cs`: GET `/{loanId}/repayments`, POST `/{loanId}/repayments/generate`, POST `/{loanId}/repayments/{repaymentId}/pay` per contracts/loans-api.yaml
- [X] T082 [US6] Add EF Core migration for `loans.repayment_entries` table with indexes per data-model.md
- [X] T083 [US6] Implement `RepaymentReceivedConsumer` in `src/services/FundManager.Contributions/Infrastructure/Consumers/RepaymentReceivedConsumer.cs` to create repayment + interest-income Transactions in fund ledger per research.md Section 2
- [X] T084 [US6] Publish `RepaymentDueGenerated`, `RepaymentRecorded`, `LoanClosed` integration events per research.md Section 2
- [X] T085 [P] [US6] Create web page: Loan Repayment Schedule page in `src/web/fund-manager-web/src/pages/loans/RepaymentSchedulePage.tsx` with month-by-month breakdown and Record Repayment modal
- [X] T086 [P] [US6] Create mobile screen: Repayment detail and payment in `src/mobile/fund-manager-mobile/src/screens/loans/RepaymentScreen.tsx`

**Checkpoint**: Reducing-balance interest calculation verified, repayment recording works with idempotency, fund ledger updated with interest income.

---

## Phase 8: User Story 5 — Loan Voting Workflow (Priority: P2)

**Goal**: Fund Admin can trigger a vote on a loan request; Editors vote within a time window; Admin finalises with optional override.

**Independent Test**: Enable voting on a fund, trigger vote on a loan, have 3 editors vote, verify tally and admin finalisation + override audit logging.

### Implementation

- [X] T087 [P] [US5] Create `VotingSession` domain entity in `src/services/FundManager.Loans/Domain/Entities/VotingSession.cs` with window, threshold, result, overrideUsed per data-model.md
- [X] T088 [P] [US5] Create `Vote` domain entity in `src/services/FundManager.Loans/Domain/Entities/Vote.cs` with VoterId, Decision (Approve/Reject), CastAt (immutable) per data-model.md
- [X] T089 [US5] Implement `VotingService` in `src/services/FundManager.Loans/Domain/Services/VotingService.cs` with StartVoting (create session, notify editors), CastVote (one per editor, immutable), FinaliseVoting (check threshold, allow override, log override) per FR-044 through FR-049
- [X] T090 [US5] Implement Voting API endpoints in `src/services/FundManager.Loans/Api/Controllers/VotingController.cs`: POST `/{loanId}/voting`, GET `/{loanId}/voting`, POST `/{loanId}/voting/vote`, POST `/{loanId}/voting/finalise` per contracts/loans-api.yaml
- [X] T091 [US5] Add EF Core migration for `loans.voting_sessions` and `loans.votes` tables per data-model.md
- [X] T092 [US5] Publish `VotingStarted`, `VoteCast`, `VotingFinalised` integration events per research.md Section 2
- [X] T093 [P] [US5] Create web page: Voting Session page in `src/web/fund-manager-web/src/pages/loans/VotingSessionPage.tsx` with vote tally visualization and finalise controls
- [X] T094 [P] [US5] Create mobile screen: Cast Vote screen in `src/mobile/fund-manager-mobile/src/screens/loans/CastVoteScreen.tsx`

**Checkpoint**: Voting workflow works end-to-end. Admin override is logged in audit trail.

---

## Phase 9: User Story 7 — Fund Dissolution & Settlement (Priority: P2)

**Goal**: Fund Admin initiates dissolution, system calculates per-member settlement, admin confirms.

**Independent Test**: Create a fund with known contributions and interest, dissolve, verify settlement calculations match expected payouts (spec.md worked example).

### Implementation

- [X] T095 [P] [US7] Create `DissolutionSettlement` domain entity in `src/services/FundManager.Dissolution/Domain/Entities/DissolutionSettlement.cs` with TotalInterestPool, TotalContributionsCollected, Status (Calculating/Reviewed/Confirmed) per data-model.md
- [X] T096 [P] [US7] Create `DissolutionLineItem` domain entity in `src/services/FundManager.Dissolution/Domain/Entities/DissolutionLineItem.cs` with per-member breakdown fields (TotalPaidContributions, InterestShare, OutstandingLoanPrincipal, UnpaidInterest, UnpaidDues, GrossPayout, NetPayout) per data-model.md
- [X] T097 [US7] Implement `DissolutionService` in `src/services/FundManager.Dissolution/Domain/Services/DissolutionService.cs` with InitiateDissolution (Active→Dissolving, block new members/loans/contributions), CalculateSettlement (fetch contributions/loans/interest via cross-service events, compute proportional interest sharing per FR-084), ConfirmDissolution (block if any netPayout<0, Dissolving→Dissolved) per FR-080 through FR-088
- [X] T098 [US7] Implement Dissolution API endpoints in `src/services/FundManager.Dissolution/Api/Controllers/DissolutionController.cs`: POST `/initiate`, GET `/settlement`, POST `/settlement/recalculate`, POST `/confirm`, GET `/report` per contracts/dissolution-api.yaml
- [X] T099 [US7] Add EF Core migration for `dissolution.dissolution_settlements` and `dissolution.dissolution_line_items` tables per data-model.md
- [X] T100 [US7] Implement settlement report export (PDF via QuestPDF and CSV) in `src/services/FundManager.Dissolution/Infrastructure/Reports/SettlementReportGenerator.cs` per FR-086
- [X] T101 [US7] Publish `DissolutionInitiated`, `SettlementCalculated`, `DissolutionConfirmed` integration events per research.md Section 2
- [X] T102 [US7] Implement `FundDissolvingConsumer` in `src/services/FundManager.FundAdmin/Infrastructure/Consumers/FundDissolvingConsumer.cs` to update Fund state to Dissolving and block new joins per research.md Section 2
- [X] T103 [P] [US7] Create web pages: Dissolution initiation page in `src/web/fund-manager-web/src/pages/dissolution/DissolutionPage.tsx` with settlement preview table and confirm button
- [X] T104 [P] [US7] Create mobile screen: Dissolution status and settlement view in `src/mobile/fund-manager-mobile/src/screens/dissolution/DissolutionScreen.tsx`

**Checkpoint**: Dissolution workflow from initiation through settlement calculation to confirmation works. Reports exportable as PDF/CSV.

---

## Phase 10: User Story 9 — Reports & Exports (Priority: P2)

**Goal**: Fund-level and member-level reports with date filtering and PDF/CSV export.

**Independent Test**: Generate a contribution summary report for a fund, verify totals match ledger data, download as PDF.

### Implementation

- [X] T105 [US9] Implement report aggregation service in `src/gateway/FundManager.ApiGateway/Services/ReportAggregationService.cs` that fetches data from Contributions, Loans, and Dissolution services to compose reports per contracts/reports-api.yaml
- [X] T106 [US9] Implement Reports API endpoints in `src/gateway/FundManager.ApiGateway/Controllers/ReportsController.cs`: GET `/contribution-summary`, GET `/loan-portfolio`, GET `/interest-earnings`, GET `/balance-sheet`, GET `/member/{userId}/statement` per contracts/reports-api.yaml
- [X] T107 [US9] Implement PDF report generation using QuestPDF in `src/gateway/FundManager.ApiGateway/Reports/PdfReportGenerator.cs`; implement CSV export in `src/gateway/FundManager.ApiGateway/Reports/CsvReportGenerator.cs` per FR-093
- [X] T108 [P] [US9] Create web pages: Reports dashboard in `src/web/fund-manager-web/src/pages/reports/ReportsPage.tsx` with report selector, date-range picker, and export buttons
- [X] T109 [P] [US9] Create mobile screen: Reports screen in `src/mobile/fund-manager-mobile/src/screens/reports/ReportsScreen.tsx` with share sheet for exports

**Checkpoint**: All 5 report types generate correctly, totals tie back to ledger data, PDF/CSV downloads work.

---

## Phase 11: User Story 10 — Notifications & Reminders (Priority: P2)

**Goal**: Multi-channel notification delivery with preferences and retry/fallback logic.

**Independent Test**: Trigger a contribution cycle, verify members receive push + email notifications. Test retry on delivery failure.

### Implementation

- [X] T110 [P] [US10] Create `Notification` domain entity in `src/services/FundManager.Notifications/Domain/Entities/Notification.cs` with Channel, TemplateKey, Payload (JSONB), Status (Pending/Sent/Failed), RetryCount, ScheduledAt, SentAt per data-model.md
- [X] T111 [P] [US10] Create `NotificationPreference` domain entity in `src/services/FundManager.Notifications/Domain/Entities/NotificationPreference.cs` per data-model.md
- [X] T112 [P] [US10] Create `DeviceToken` domain entity in `src/services/FundManager.Notifications/Domain/Entities/DeviceToken.cs` per data-model.md
- [X] T113 [US10] Implement `NotificationDispatchService` in `src/services/FundManager.Notifications/Domain/Services/NotificationDispatchService.cs` with dispatch logic: check preferences, select channel, send via provider, retry (3 attempts exponential backoff), fallback (push→email→in-app) per FR-100, FR-104, FR-105
- [X] T114 [US10] Implement notification template engine in `src/services/FundManager.Notifications/Infrastructure/Templates/NotificationTemplateEngine.cs` with placeholder substitution per FR-103
- [X] T115 [US10] Implement MassTransit consumers for all notification triggers: `ContributionDueGeneratedConsumer`, `ContributionPaidConsumer`, `LoanApprovedConsumer`, `LoanRejectedConsumer`, `RepaymentDueConsumer`, `VotingStartedConsumer`, `DissolutionInitiatedConsumer` in `src/services/FundManager.Notifications/Infrastructure/Consumers/` per spec.md Notification Rules
- [X] T116 [US10] Implement Notifications API endpoints in `src/services/FundManager.Notifications/Api/Controllers/NotificationsController.cs`: GET `/feed`, GET `/feed/unread-count`, GET/PUT `/preferences`, POST/DELETE `/devices` per contracts/notifications-api.yaml
- [X] T117 [US10] Add EF Core migration for `notifications.notifications`, `notifications.notification_preferences`, `notifications.device_tokens` tables (quarterly partitioned for notifications) per data-model.md
- [X] T118 [P] [US10] Create web component: Notification bell with dropdown in `src/web/fund-manager-web/src/components/notifications/NotificationBell.tsx`
- [X] T119 [P] [US10] Create mobile screen: Notification feed in `src/mobile/fund-manager-mobile/src/screens/notifications/NotificationFeedScreen.tsx` and preferences in `src/mobile/fund-manager-mobile/src/screens/notifications/NotificationPreferencesScreen.tsx`

**Checkpoint**: Notifications fire for all key events, retry/fallback works, user preferences respected.

---

## Phase 12: User Story 11 — Audit Trail & Security (Priority: P2)

**Goal**: Every state-changing action logged with actor/timestamp/before/after. Audit query API functional.

**Independent Test**: Approve a loan, verify AuditLog entry with correct before/after state. Query entity history timeline.

### Implementation

- [X] T120 [US11] Implement audit log query endpoints in `src/services/FundManager.Audit/Api/Controllers/AuditController.cs`: GET `/logs`, GET `/logs/{logId}`, GET `/entity-history` per contracts/audit-api.yaml
- [X] T121 [US11] Wire `AuditEventPublisher` (from T021) into all domain services across Identity, FundAdmin, Contributions, Loans, Dissolution services — ensure every state-changing operation publishes audit events with before/after state per FR-110
- [X] T122 [P] [US11] Create web page: Audit Log viewer in `src/web/fund-manager-web/src/pages/audit/AuditLogPage.tsx` with date range, actor, and entity filters per spec.md UI Screens
- [X] T123 [P] [US11] Create web page: Entity History timeline in `src/web/fund-manager-web/src/pages/audit/EntityHistoryPage.tsx`

**Checkpoint**: All actions produce audit entries. Query API returns correct results with partition pruning.

---

## Phase 13: User Story 8 — Guest Views Fund Summary (Priority: P3)

**Goal**: Guest users can view all fund data in read-only mode with no action capabilities.

**Independent Test**: Log in as Guest, verify dashboard/contributions/loans/reports render data but all create/edit/delete buttons are hidden and API rejects writes.

### Implementation

- [X] T124 [US8] Implement `FundGuest` authorization policy enforcement across all API controllers — verify Guest role returns 403 for all mutating endpoints per FR-111, spec.md Section 4 Role Matrix
- [X] T125 [US8] Implement frontend role-based UI gating in `src/web/fund-manager-web/src/hooks/usePermissions.ts` and `src/mobile/fund-manager-mobile/src/hooks/usePermissions.ts` to hide action buttons for Guest role per spec.md Section 4
- [X] T126 [P] [US8] Update all web pages to conditionally render action buttons based on `usePermissions()` hook: FundDashboardPage, ContributionDuesPage, LoanApprovalPage, ReportsPage per spec.md UI
- [X] T127 [P] [US8] Update all mobile screens to conditionally render action buttons based on `usePermissions()` hook per spec.md Mobile UI

**Checkpoint**: Guest user sees all data in read-only mode. API rejects any mutation attempt with 403.

---

## Phase 14: Overdue Handling & Penalties

**Purpose**: Cross-cutting overdue detection, reminders, and configurable penalties

- [X] T128 Implement overdue detection scheduled job in `src/services/FundManager.Contributions/Infrastructure/Jobs/OverdueDetectionJob.cs` using background service / Hangfire to periodically Mark Late and Mark Missed per FR-033, FR-034
- [X] T129 Implement repayment overdue detection job in `src/services/FundManager.Loans/Infrastructure/Jobs/RepaymentOverdueJob.cs` with reminder notifications at 3, 7, 14 days per FR-070, FR-071
- [X] T130 Implement penalty calculation in `src/services/FundManager.Loans/Domain/Services/PenaltyService.cs` with flat or percentage penalty on overdue amount, added to next month's RepaymentEntry per FR-072, FR-073

**Checkpoint**: Overdue detection, penalty calculation, and reminder notifications all operational.

---

## Phase 15: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T131 [P] Implement API rate limiting in `src/gateway/FundManager.ApiGateway/` using ASP.NET Core rate-limiting middleware (5 OTP requests per 15 min, general 100 req/min) per NFR-006
- [X] T132 [P] Add OpenAPI/Swagger documentation generation to all service Api projects per quickstart.md Section 8
- [X] T133 [P] Implement frontend error boundary and global error handling in `src/web/fund-manager-web/src/components/ErrorBoundary.tsx` and `src/mobile/fund-manager-mobile/src/components/ErrorBoundary.tsx`
- [X] T134 [P] Implement Login/OTP screens: `src/web/fund-manager-web/src/pages/auth/LoginPage.tsx` and `src/mobile/fund-manager-mobile/src/screens/auth/LoginScreen.tsx` with OTP request/verify flow
- [X] T135 [P] Implement navigation and auth guard in web app (`src/web/fund-manager-web/src/components/AuthGuard.tsx`) and mobile app (`src/mobile/fund-manager-mobile/src/navigation/AuthNavigator.tsx`)
- [X] T136 [P] Add `infrastructure/docker-compose.dev.yml` with dev overrides: hot reload, debug ports, volume mounts per quickstart.md Section 9
- [X] T137 Code cleanup: ensure consistent error response format (ProblemDetails RFC 7807) across all services
- [X] T138 Performance review: add Redis caching for fund dashboard aggregations, member lists, and report data per research.md
- [X] T139 Run quickstart.md validation: verify full stack starts from clean clone per quickstart.md

---

## Phase 16: Logout Functionality

**Purpose**: Wire frontend logout to backend session revocation endpoint; add logout UI to web and mobile apps

- [X] T140 [P] Update web `authStore.ts` (`src/web/fund-manager-web/src/stores/authStore.ts`) to call `POST /auth/logout` backend endpoint before clearing localStorage and local state; add `isLoggingOut` loading state to prevent double-clicks
- [X] T141 [P] Update web `apiClient.ts` (`src/web/fund-manager-web/src/services/apiClient.ts`) 401 interceptor to clear Zustand auth state in addition to localStorage on unauthorized response
- [X] T142 [P] Create `AppHeader` component (`src/web/fund-manager-web/src/components/AppHeader.tsx`) with app title link and logout button; shows loading spinner during logout
- [X] T143 [P] Create `AuthenticatedLayout` wrapper component (`src/web/fund-manager-web/src/components/AuthenticatedLayout.tsx`) that renders `AppHeader` above page content
- [X] T144 Update `App.tsx` (`src/web/fund-manager-web/src/App.tsx`) to wrap all authenticated routes with `AuthenticatedLayout` so the header with logout button appears on every protected page
- [X] T145 [P] Update mobile `authStore.ts` (`src/mobile/fund-manager-mobile/src/stores/authStore.ts`) to call `POST /auth/logout` backend endpoint before clearing in-memory state; add `isLoggingOut` flag
- [X] T146 [P] Update mobile `HomeScreen.tsx` (`src/mobile/fund-manager-mobile/src/screens/HomeScreen.tsx`) to add header bar with logout button; show ActivityIndicator during logout

**Checkpoint**: Logout button visible on all authenticated web pages (in header) and on mobile home screen. Clicking logout revokes the server-side session via `POST /api/auth/logout`, then clears local state and redirects to login.

---

## Phase 17: Update Fund UI

**Purpose**: Add frontend UI for editing fund description (always mutable) and full fund configuration (mutable in Draft state per FR-011). Backend `PATCH /api/funds/{fundId}` and web `useUpdateFund` hook already exist.

- [X] T147 [P] Create `EditDescriptionModal` component in `src/web/fund-manager-web/src/pages/funds/EditDescriptionModal.tsx` with textarea, character counter (500 max), save/cancel buttons, loading/error states; uses existing `useUpdateFund` hook
- [X] T148 Update `FundDashboardPage.tsx` (`src/web/fund-manager-web/src/pages/funds/FundDashboardPage.tsx`) to show edit (pencil) icon next to fund description for Fund Admins; clicking opens `EditDescriptionModal` (non-Draft) or `EditFundConfigModal` (Draft); show "No description" placeholder when empty
- [X] T149 [P] Create `FundDetailScreen` for mobile (`src/mobile/fund-manager-mobile/src/screens/funds/FundDetailScreen.tsx`) with fund info, dashboard stats, configuration display, edit config modal (for Draft-state Admins), edit description modal (for non-Draft Admins), and activate button (for Draft funds); uses `usePermissions` for role gating
- [X] T150 Update `RootNavigator.tsx` (`src/mobile/fund-manager-mobile/src/navigation/RootNavigator.tsx`) to register `FundDetail` route with `FundDetailScreen` component; add `fundId` param type to `RootStackParamList`
- [X] T151 Update `FundListScreen.tsx` (`src/mobile/fund-manager-mobile/src/screens/funds/FundListScreen.tsx`) to navigate to `FundDetail` screen on fund card press; add `useNavigation` import

**Checkpoint**: Fund Admins can edit fund description via web dashboard (pencil icon → modal) and mobile fund detail screen (Edit button → modal). Draft-state funds show full configuration editing via `EditFundConfigModal` (web) and inline config modal (mobile). Changes are persisted via `PATCH /api/funds/{fundId}` and reflected immediately.

---

## Phase 19: Draft Fund Configuration Editability

**Purpose**: Enable Fund Admins to edit all fund configuration fields while the fund is in Draft state, per updated FR-011. Once activated, all config fields except description become immutable. Backend domain logic, expanded DTOs, conditional validation, and frontend editing UI.

**Prerequisites**: Phase 3 (US1 — Fund entity, FundService, PATCH endpoint), Phase 17 (Update Fund UI)

**Independent Test**: Create a fund (Draft state), edit config fields (name, interest rate, etc.) via PATCH, verify changes persist. Activate fund, attempt same edits → 409 Conflict. Edit description on Active fund → succeeds.

### Implementation

- [X] T176 [US1] Add `UpdateConfiguration()` domain method to `Fund` entity (`src/services/FundManager.FundAdmin/Domain/Entities/Fund.cs`) accepting all config fields as nullable parameters; gate on `State == Draft` (return failure if not Draft); validate each field individually (rate > 0, contribution > 0, day 1–28, etc.); apply changes and call `SetUpdated()` per FR-011
- [X] T177 [US1] Update `FundService.UpdateFundAsync` in `src/services/FundManager.FundAdmin/Domain/Services/FundService.cs` to accept expanded `UpdateFundRequestDto`; handle description separately (always allowed, any state); detect config field changes and call `fund.UpdateConfiguration()` for Draft-state funds; capture full before/after audit state per FR-011
- [X] T178 [US1] Expand `UpdateFundRequestDto` in `src/services/FundManager.FundAdmin/Api/Controllers/FundsController.cs` to include all config fields (name, monthlyInterestRate, minimumMonthlyContribution, minimumPrincipalPerRepayment, currency, loanApprovalPolicy, maxLoanPerMember, maxConcurrentLoans, dissolutionPolicy, overduePenaltyType, overduePenaltyValue, contributionDayOfMonth, gracePeriodDays) plus `ClearMaxLoanPerMember`/`ClearMaxConcurrentLoans` boolean flags for nullable field clearing; update `UpdateFund` action to return 409 Conflict for INVALID_STATE errors
- [X] T179 [US1] Add `DissolutionPolicy` property to `FundDto` class and `MapToDto()` method in `src/services/FundManager.FundAdmin/Api/Controllers/FundsController.cs` to include dissolution policy in API responses
- [X] T180 [US1] Update `UpdateFundValidator` in `src/services/FundManager.FundAdmin/Api/Validators/FundValidators.cs` with comprehensive conditional FluentValidation rules for all config fields: Name max 255, rates > 0 and ≤ 1, contribution/principal > 0, valid policy enums, day 1–28, grace period ≥ 0, etc. (rules applied only when the field is present in the request)
- [X] T181 [P] Expand `UpdateFundRequest` TypeScript type in `src/web/fund-manager-web/src/types/fund.ts` to include all config fields plus `clearMaxLoanPerMember` and `clearMaxConcurrentLoans` booleans; add `dissolutionPolicy` to `Fund` interface
- [X] T182 [P] Create `EditFundConfigModal` component in `src/web/fund-manager-web/src/pages/funds/EditFundConfigModal.tsx` with form fields for all config properties, pre-populated from current fund values, change detection (only sends modified fields), percentage-to-decimal conversion for rates, nullable field clearing flags, `FieldGroup` component with `<label>` and `aria-label` for accessibility, and Draft-only warning message
- [X] T183 Update `FundDashboardPage.tsx` (`src/web/fund-manager-web/src/pages/funds/FundDashboardPage.tsx`) to import `EditFundConfigModal`, add `editConfigOpen` state, route description edit pencil to config modal for Draft / description modal for non-Draft, and add "Edit Configuration" button in config section header for Draft funds
- [X] T184 [P] Update `FundDetailScreen.tsx` (mobile) (`src/mobile/fund-manager-mobile/src/screens/funds/FundDetailScreen.tsx`) to add `EditConfigModal` inline component with full config form, `ConfigField` helper component, `editConfigModalVisible` state, "Edit" button in config section for Draft funds, and route to config modal for Draft / description modal for non-Draft
- [X] T185 Update `spec.md` (FR-011, acceptance scenarios), `fundadmin-api.yaml` (UpdateFundRequest schema, 409 response), and `data-model.md` (immutability rule, column annotations, business constraints) to reflect Draft-state editability per updated FR-011

**Checkpoint**: Fund configuration fields are fully editable in Draft state via web and mobile. Activation locks all config fields except description. Backend returns 409 Conflict when attempting config edits on non-Draft funds. Audit trail captures before/after state for all config changes.

---

## Phase 18: Notification Channel Providers & OTP Delivery

**Purpose**: Wire real channel delivery (SMS, email, push) into the existing notification dispatch pipeline. Build vendor-agnostic provider abstractions, mock implementations for dev, MailHog for email testing, and OTP delivery via the Notifications service.

**Research**: [research-notifications.md](research-notifications.md)

**Prerequisites**: Phase 11 (US10 — Notifications service built), Phase 2 (Identity service with OTP auth)

**Independent Test**: Request an OTP → verify it's NOT in the response body, check console logs for SMS content. Trigger a contribution payment → verify email appears in MailHog web UI at `http://localhost:8025`. Check `/api/notifications/feed` → in_app notifications still appear.

### Step 1: Provider Abstraction Interfaces

- [ ] T152 [P] Create `ISmsSender` interface in `src/shared/FundManager.BuildingBlocks/Notifications/ISmsSender.cs` with method `Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct)` per FR-106, research-notifications.md Section 7
- [ ] T153 [P] Create `IEmailSender` interface in `src/shared/FundManager.BuildingBlocks/Notifications/IEmailSender.cs` with method `Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken ct)` per FR-106, research-notifications.md Section 7
- [ ] T154 [P] Create `IPushNotificationSender` interface in `src/shared/FundManager.BuildingBlocks/Notifications/IPushNotificationSender.cs` with method `Task<bool> SendAsync(string deviceToken, string platform, string title, string body, CancellationToken ct)` per FR-106, research-notifications.md Section 7
- [ ] T155 [P] Create `ChannelContact` record and `IRecipientResolver` interface in `src/shared/FundManager.BuildingBlocks/Notifications/IRecipientResolver.cs` with `ChannelContact` holding `Email`, `Phone`, `IReadOnlyList<DeviceTokenInfo>` and method `Task<ChannelContact?> ResolveAsync(Guid recipientId, CancellationToken ct)` per FR-107, research-notifications.md Section 8

### Step 2: Mock/Dev Provider Implementations

- [ ] T156 [P] Create `ConsoleSmsSender` in `src/services/FundManager.Notifications/Infrastructure/Providers/ConsoleSmsSender.cs` implementing `ISmsSender`; logs SMS content to `ILogger` at Information level with format `SMS → {PhoneNumber}: {Message}`; always returns `true` per FR-109, research-notifications.md Section 6
- [ ] T157 [P] Create `SmtpEmailSender` in `src/services/FundManager.Notifications/Infrastructure/Providers/SmtpEmailSender.cs` implementing `IEmailSender`; uses `System.Net.Mail.SmtpClient` configured from `Email:SmtpHost` and `Email:SmtpPort` settings; sends from `Email:FromAddress` / `Email:FromName`; returns `true` on success, `false` on `SmtpException` per FR-109, research-notifications.md Section 4
- [ ] T158 [P] Create `ConsolePushSender` in `src/services/FundManager.Notifications/Infrastructure/Providers/ConsolePushSender.cs` implementing `IPushNotificationSender`; logs push content to `ILogger` with format `PUSH → {DeviceToken} ({Platform}): {Title} — {Body}`; always returns `true` per FR-109, research-notifications.md Section 6

### Step 3: Recipient Contact Resolution

- [ ] T159 Create `HttpRecipientResolver` in `src/services/FundManager.Notifications/Infrastructure/Providers/HttpRecipientResolver.cs` implementing `IRecipientResolver`; fetches user email/phone via `HttpClient` call to Identity service at `GET /api/users/{recipientId}/profile`; fetches device tokens from local `NotificationsDbContext.DeviceTokens`; returns `ChannelContact` with resolved data or `null` if user not found per FR-107, research-notifications.md Section 8
- [ ] T160 [P] Create `GET /api/users/{userId}/profile` internal endpoint in `src/services/FundManager.Identity/Api/Controllers/ProfileController.cs` (or verify it exists) returning `{ id, name, email, phone }` for cross-service resolution; endpoint requires no auth (internal service-to-service call) or uses service-level JWT per research-notifications.md Section 8

### Step 4: Wire Providers into Dispatch Service

- [ ] T161 Refactor `NotificationDispatchService` in `src/services/FundManager.Notifications/Domain/Services/NotificationDispatchService.cs`: inject `ISmsSender`, `IEmailSender`, `IPushNotificationSender`, and `IRecipientResolver` via constructor; replace the stubbed `SendViaChannelAsync` method with real channel routing: resolve recipient contact via `IRecipientResolver`, then route `"sms"` → `ISmsSender.SendAsync(contact.Phone, body, ct)`, `"email"` → `IEmailSender.SendAsync(contact.Email, title, body, ct)`, `"push"` → iterate `contact.DeviceTokens` and call `IPushNotificationSender.SendAsync(...)` for each, `"in_app"` → return `true` (already persisted in DB); return `false` if contact info is missing for the requested channel (triggers existing fallback logic) per FR-106, FR-107, research-notifications.md Section 7

### Step 5: OTP Delivery via Notifications Service

- [ ] T162 [P] Add `OtpRequested` integration event record to `src/shared/FundManager.Contracts/Events/IntegrationEvents.cs`: `record OtpRequested(Guid Id, string Channel, string Target, string Otp, DateTime ExpiresAt, DateTime OccurredAt) : IIntegrationEvent` per FR-108, research-notifications.md Section 9
- [ ] T163 Update `AuthController.RequestOtp` in `src/services/FundManager.Identity/Api/Controllers/AuthController.cs`: inject `IPublishEndpoint` (MassTransit); after `_otpService.RequestOtpAsync(...)`, publish `OtpRequested` event with plaintext OTP, channel, and target; **remove the plaintext OTP from the response body** — change `Message` to only show masked target (e.g., `"OTP sent to ****3210"`); this implements FR-108 and fixes the security gap per research-notifications.md Section 9
- [ ] T164 [P] Add `"otp.requested"` template to `NotificationTemplateEngine.cs` in `src/services/FundManager.Notifications/Infrastructure/Templates/NotificationTemplateEngine.cs`: title `"Your Verification Code"`, body `"Your verification code is {{otp}}. It expires in 5 minutes. Do not share this code with anyone."` per FR-103, research-notifications.md Section 9
- [ ] T165 Create `OtpRequestedConsumer` in `src/services/FundManager.Notifications/Infrastructure/Consumers/OtpRequestedConsumer.cs` implementing `IConsumer<OtpRequested>`: resolve channel (`"phone"` → `ISmsSender`, `"email"` → `IEmailSender`); render `"otp.requested"` template with `{{otp}}` placeholder; send directly via provider (bypass full dispatch pipeline for OTP speed — no retry/fallback needed since the user can re-request); log delivery result per FR-108, research-notifications.md Section 9

### Step 6: Docker & Configuration

- [ ] T166 Add MailHog service to `infrastructure/docker-compose.yml`: image `mailhog/mailhog:latest`, ports `8025:8025` (web UI), internal SMTP at `1025`; add to `fundmanager` network; make `notifications` service depend on `mailhog` (service_started) per FR-109, research-notifications.md Section 6
- [ ] T167 [P] Update `src/services/FundManager.Notifications/appsettings.Docker.json`: add `"Email": { "Provider": "Smtp", "SmtpHost": "mailhog", "SmtpPort": 1025, "FromAddress": "noreply@fundmanager.local", "FromName": "FundManager" }`, `"Sms": { "Provider": "Console" }`, `"Push": { "Provider": "Console" }` per research-notifications.md Section 10
- [ ] T168 [P] Update `src/services/FundManager.Notifications/appsettings.Development.json`: add same config as T167 but with `"SmtpHost": "localhost"` per research-notifications.md Section 10
- [ ] T169 [P] Add named `HttpClient` registration for Identity service in `src/services/FundManager.Notifications/Program.cs`: `builder.Services.AddHttpClient("IdentityService", c => c.BaseAddress = new Uri("http://identity:8080"))` (Docker) per research-notifications.md Section 8

### Step 7: DI Registration & Wiring

- [ ] T170 Update `src/services/FundManager.Notifications/Program.cs`: register `ISmsSender` → `ConsoleSmsSender`, `IEmailSender` → `SmtpEmailSender`, `IPushNotificationSender` → `ConsolePushSender`, `IRecipientResolver` → `HttpRecipientResolver` in DI container; read configuration from `Email`, `Sms`, `Push` sections per FR-106, research-notifications.md Section 7
- [ ] T171 [P] Ensure `MassTransitConfig` in `src/shared/FundManager.BuildingBlocks/Messaging/` auto-discovers `OtpRequestedConsumer` in the Notifications service assembly; verify consumer registration in MassTransit's `AddConsumers` call per research-notifications.md Section 9
- [ ] T172 [P] Inject `IPublishEndpoint` into `AuthController` constructor in Identity service; verify MassTransit publisher is registered in `src/services/FundManager.Identity/Program.cs` (should already be registered via `AddMassTransitWithRabbitMq`) per FR-108

### Step 8: Fund-Wide Broadcast Resolution (Enhancement)

- [ ] T173 [P] Create `IFundMemberResolver` interface in `src/shared/FundManager.BuildingBlocks/Notifications/IFundMemberResolver.cs` with method `Task<List<Guid>> GetMemberIdsAsync(Guid fundId, CancellationToken ct)` for resolving all member user IDs of a fund
- [ ] T174 Create `HttpFundMemberResolver` in `src/services/FundManager.Notifications/Infrastructure/Providers/HttpFundMemberResolver.cs` implementing `IFundMemberResolver`; calls FundAdmin service at `GET /api/funds/{fundId}/members` and extracts user IDs from the member list response
- [ ] T175 Update fund-wide broadcast consumers in `src/services/FundManager.Notifications/Infrastructure/Consumers/NotificationConsumers.cs` (`ContributionDueGeneratedConsumer`, `LoanRejectedConsumer`, `RepaymentDueConsumer`, `VotingStartedConsumer`, `DissolutionInitiatedConsumer`): inject `IFundMemberResolver`; replace `Guid.Empty` recipient placeholder with actual member list resolution; loop and dispatch to each member individually per FR-100

**Checkpoint**: All notification channels deliver via real providers (console SMS, MailHog email, console push for dev). OTP is sent via SMS/email through the Notifications service and NOT leaked in the API response. MailHog web UI shows captured emails at `http://localhost:8025`. Fund-wide broadcasts resolve to individual member notifications.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Phase 2 — No dependencies on other stories
- **US2 (Phase 4)**: Depends on Phase 2 + US1 (needs Fund entity)
- **US3 (Phase 5)**: Depends on Phase 2 + US2 (needs members to generate dues)
- **US4 (Phase 6)**: Depends on Phase 2 + US3 (needs fund pool from contributions)
- **US6 (Phase 7)**: Depends on US4 (needs active loans)
- **US5 (Phase 8)**: Depends on US4 (needs loan in PendingApproval) — can parallel with US6
- **US7 (Phase 9)**: Depends on US3 + US6 (needs contributions + loans data)
- **US9 (Phase 10)**: Depends on US3 + US6 (needs data to report on) — can parallel with US7
- **US10 (Phase 11)**: Depends on Phase 2 — can start after foundational but best after US3 (to test notification triggers)
- **US11 (Phase 12)**: Depends on Phase 2 — can run in parallel with any story (audit is cross-cutting)
- **US8 (Phase 13)**: Depends on at least US1 + US3 (needs pages to view)
- **Overdue (Phase 14)**: Depends on US3 + US6
- **Polish (Phase 15)**: Depends on all desired phases complete
- **Logout (Phase 16)**: Depends on Phase 2 (Identity service with logout endpoint) + Phase 15 (Login/auth guard in place)
- **Update Fund UI (Phase 17)**: Depends on Phase 3 (US1 — Fund entity + PATCH endpoint + web hook) + Phase 15 (frontend scaffolding)
- **Notification Providers (Phase 18)**: Depends on Phase 11 (US10 — Notifications service) + Phase 2 (Identity service with OTP)
- **Draft Config Editability (Phase 19)**: Depends on Phase 3 (US1 — Fund entity + PATCH endpoint) + Phase 17 (Update Fund UI)

### User Story Dependency Graph

```
Phase 1 (Setup)
    └── Phase 2 (Foundational) ──BLOCKS──┐
            ├── US1 (Fund Create)         │
            │    └── US2 (Membership)     │
            │         └── US3 (Contributions)
            │              ├── US4 (Loans)│
            │              │    ├── US5 (Voting) ←── can parallel
            │              │    └── US6 (Repayment)
            │              ├── US7 (Dissolution) ←── needs US3+US6
            │              └── US9 (Reports) ←── needs US3+US6
            ├── US10 (Notifications) ←── can start early
            ├── US11 (Audit) ←── can start early
            └── US8 (Guest Views) ←── needs pages
                     └── Phase 14 (Overdue) ←── US3+US6
                              └── Phase 15 (Polish)
                                       ├── Phase 16 (Logout) ←── needs auth UI
                                       └── Phase 17 (Update Fund UI) ←── needs fund pages
                                                └── Phase 19 (Draft Config Editability) ←── needs Phase 3+17
            └── Phase 18 (Notification Providers) ←── needs US10+Phase 2
```

### Parallel Opportunities

Within each phase, tasks marked **[P]** can run in parallel:
- **Phase 1**: T002–T008 all parallel (different projects)
- **Phase 2**: T010–T015 parallel (7 services); T017–T019 parallel; T023–T025 parallel
- **Phase 3**: T028–T030 parallel (entities); T035–T036 parallel; T043–T045 parallel (web/mobile)
- **Phase 5**: T055–T056 parallel; T066–T068 parallel
- **Phase 8**: T087–T088 parallel
- **Phase 11**: T110–T112 parallel
- **Phase 19**: T176+T180 parallel (entity+validator); T181+T182+T184 parallel (web+mobile)

**Cross-phase parallelism**: Once Phase 2 completes, US10 (Notifications) and US11 (Audit) can start in parallel with the P1 stories.

---

## Parallel Example: User Story 3 (Contributions)

```
# Parallel batch 1 — Entities (different files):
T055: Create ContributionDue entity
T056: Create Transaction entity

# Sequential — Services (depend on entities):
T057: ContributionCycleService (depends on T055)
T058: PaymentService (depends on T055, T056)
T059: OverdueDetectionService (depends on T055)

# Sequential — API (depends on services):
T060: Dues controller
T061: Payments controller
T062: Ledger controller

# Parallel batch 2 — Frontend (different apps):
T066: Web contributions page
T067: Web ledger page
T068: Mobile dues screen
```

---

## Implementation Strategy

### MVP First (P1 Stories Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US1 — Fund Creation
4. Complete Phase 4: US2 — Membership
5. Complete Phase 5: US3 — Contributions
6. Complete Phase 6: US4 — Loan Requests
7. Complete Phase 7: US6 — Repayments
8. **STOP and VALIDATE**: Core fund lifecycle (create → join → contribute → lend → repay) works end-to-end
9. Deploy/demo if ready — this is the MVP

### Incremental Delivery (P2 Stories)

10. Add Phase 8: US5 — Voting (enhances loan approval)
11. Add Phase 9: US7 — Dissolution (fund lifecycle completion)
12. Add Phase 10: US9 — Reports (transparency)
13. Add Phase 11: US10 — Notifications (engagement)
14. Add Phase 12: US11 — Audit Trail (compliance)
15. Add Phase 14: Overdue handling (enforcement)

### P3 & Polish

16. Add Phase 13: US8 — Guest Views
17. Complete Phase 15: Polish
18. Complete Phase 16: Logout (wire frontend to backend session revocation)
19. Complete Phase 17: Update Fund UI (edit description on web and mobile)
20. Complete Phase 19: Draft Config Editability (edit full config in Draft state)

---

## Summary

| Metric | Count |
|--------|-------|
| **Total tasks** | 185 |
| **Setup tasks** | 8 (T001–T008) |
| **Foundational tasks** | 19 (T009–T027) |
| **US1 — Fund Creation** | 18 (T028–T045) |
| **US2 — Membership** | 9 (T046–T054) |
| **US3 — Contributions** | 14 (T055–T068) |
| **US4 — Loans** | 9 (T069–T077) |
| **US6 — Repayments** | 9 (T078–T086) |
| **US5 — Voting** | 8 (T087–T094) |
| **US7 — Dissolution** | 10 (T095–T104) |
| **US9 — Reports** | 5 (T105–T109) |
| **US10 — Notifications** | 10 (T110–T119) |
| **US11 — Audit Trail** | 4 (T120–T123) |
| **US8 — Guest Views** | 4 (T124–T127) |
| **Overdue Handling** | 3 (T128–T130) |
| **Polish** | 9 (T131–T139) |
| **Logout** | 7 (T140–T146) |
| **Update Fund UI** | 5 (T147–T151) |
| **Notification Providers** | 24 (T152–T175) |
| **Draft Config Editability** | 10 (T176–T185) |
| **Phases** | 19 |
| **Parallelizable tasks** | 86 (marked [P]) |

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story phase should be independently testable at its checkpoint
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All monetary calculations use `MoneyMath` utility from BuildingBlocks (banker's rounding)
- All state-changing operations publish audit events via MassTransit
- All payment/repayment endpoints require Idempotency-Key header
