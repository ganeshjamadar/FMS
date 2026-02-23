# Feature Specification: Generic Multi-Fund Monthly Contribution & Reducing-Balance Lending System

**Feature Branch**: `001-fund-lending-system`  
**Created**: 2026-02-20  
**Status**: Draft  
**Input**: User description: "Create a complete Product Requirements Document + Functional Specification for a Generic Multi-Fund Monthly Contribution + Reducing-Balance Lending System with Web + Mobile clients."

---

## 1. Overview & Assumptions

### Overview

A community fund management platform enabling groups of people to pool monthly monetary contributions, lend pooled money to members at a reducing-balance interest rate, track repayments, and ultimately dissolve the fund with fair distribution of contributions and earned interest. The platform supports multiple independent funds, each acting as its own tenant boundary. Users may participate in many funds simultaneously with different roles in each.

The system provides both a **Web client** (optimised for fund administration) and a **Mobile client** (optimised for member day-to-day interactions).

### Assumptions

- **Currency**: INR only for MVP; no multi-currency support.
- **Multi-tenant boundary**: Each fund is an isolated tenant; cross-fund data is never mixed in ledgers or reports.
- **Authentication**: Phone or email OTP-based authentication (no password). Session tokens issued after OTP verification.
- **Interest method**: Reducing (diminishing) balance only; no flat-rate or compound interest.
- **Contribution cycle**: Monthly only; no weekly or custom cycles.
- **Time zone**: All date/time in IST (Asia/Kolkata) for MVP.
- **Notifications**: Push notifications (mobile), in-app, email, and SMS channels available; SMS for critical alerts only.
- **Payment recording**: Manual entry by Fund Admin or Editor for MVP; optional payment gateway integration as a future enhancement.
- **Rounding**: All monetary calculations rounded to nearest paisa (2 decimal places), using banker's rounding (round half to even).
- **Data retention**: Fund data retained for 7 years after dissolution per Indian financial record-keeping norms.
- **Performance**: System designed for up to 1,000 members per fund and 100+ concurrent funds.
- **Idempotency**: All payment and repayment posting endpoints accept a client-generated idempotency key to prevent duplicate transactions.

---

## Clarifications

### Session 2026-02-20

- Q: What happens to existing active loans when a fund enters Dissolving state? → A: Borrowers continue normal monthly repayments; dissolution confirmation requires all loans Closed or deductible without negative payout.
- Q: What is the system availability / uptime SLA target? → A: 99.5% (~43.8 hours/year downtime), appropriate for an early-stage community fund tool.
- Q: How should the system handle notification delivery failures? → A: Retry primary channel (3 attempts with exponential backoff), then fall back to next channel in priority order (push → email → in-app).
- Q: Should the INR 1,000 minimum principal be fixed system-wide or configurable per fund? → A: Fund-level configurable with INR 1,000 as the default.
- Q: How should the system handle concurrent payment recording for the same due by two users? → A: Optimistic locking — first write wins; second gets a conflict error and must refresh & retry.

### Session 2026-02-21 — Notification Channel Providers & OTP Delivery

- Q: Which SMS provider should be used for OTP and critical notifications? → A: **Mock/Console logger for development**. Build a vendor-agnostic `ISmsSender` abstraction in BuildingBlocks. Twilio is the preferred production provider (to be integrated later).
- Q: Which email provider should be used for notifications? → A: **MailHog (SMTP capture) for development**. Build a vendor-agnostic `IEmailSender` abstraction. SendGrid is the preferred production provider (to be integrated later).
- Q: Which push notification provider should be used? → A: **Firebase Cloud Messaging (FCM)** — stubbed as console logger for dev. FCM integrates with Expo/React Native for the mobile app.
- Q: How should OTP be delivered to the user? → A: The Identity service publishes an `OtpRequested` integration event to RabbitMQ. The Notifications service consumes it and delivers via `ISmsSender` (phone channel) or `IEmailSender` (email channel). The plaintext OTP MUST NOT be returned in the API response — only a masked confirmation message like "OTP sent to +91****3210".
- Q: Should MailHog be added to Docker Compose for dev email testing? → A: Yes. MailHog captures all SMTP emails at port 1025 and provides a web UI at port 8025 for viewing.
- Q: How should the Notifications service resolve a user's email/phone from a recipientId? → A: Via HTTP call to the Identity service's internal profile endpoint. Device tokens are queried from the Notifications service's own database.

---

## 2. User Scenarios & Testing

### User Story 1 — Platform Administrator Creates & Assigns a Fund (Priority: P1)

A Platform Administrator creates a new fund on the platform, configures its parameters (name, interest rate, minimum contribution, loan policy), and assigns a Fund Admin to manage it.

**Why this priority**: Without fund creation there is no product. This is the genesis action.

**Independent Test**: Can be fully tested by creating a fund through the admin panel and verifying all configuration fields persist correctly.

**Acceptance Scenarios**:

1. **Given** a logged-in Platform Administrator, **When** they fill in fund name, description, monthly interest rate, minimum contribution, and loan approval policy and submit, **Then** a new fund is created in Draft state with all fields saved.
2. **Given** a newly created fund in Draft state, **When** the Platform Administrator assigns a registered user as Fund Admin, **Then** that user receives the Admin role for the fund and is notified.
3. **Given** a fund in Draft state, **When** the Fund Admin activates it, **Then** the fund transitions to Active state and becomes joinable by members.
4. **Given** a fund in Draft state, **When** the Fund Admin edits configuration fields (name, interest rate, minimum contribution, loan policy, penalties, etc.), **Then** the system saves the changes and returns the updated fund.
5. **Given** a fund in Active state, **When** any user attempts to change fund configuration fields other than description, **Then** the system rejects the change with an error indicating that configuration is immutable after activation.
6. **Given** a fund in Active state, **When** the Fund Admin edits the fund description, **Then** the system saves the change (description is always mutable regardless of fund state).

---

### User Story 2 — Fund Admin Manages Membership (Priority: P1)

A Fund Admin invites members, members join by selecting a fixed monthly contribution amount (≥ fund minimum), and the Fund Admin can remove members subject to restrictions.

**Why this priority**: Membership is foundational — contributions and loans depend on members existing.

**Independent Test**: Can be tested by inviting a user, having them join with a contribution amount, and verifying their membership record.

**Acceptance Scenarios**:

1. **Given** an Active fund, **When** the Fund Admin invites a user by phone number or email, **Then** the user receives an invitation notification and can accept/decline.
2. **Given** an invitation to a fund with minimum contribution INR 500, **When** a user accepts and selects INR 1,000 as their monthly contribution, **Then** their membership is created with contribution amount INR 1,000 (immutable).
3. **Given** a user tries to join with contribution INR 300 (below fund minimum INR 500), **When** they submit, **Then** the system rejects with a clear error.
4. **Given** a member with an outstanding loan, **When** the Fund Admin tries to remove them, **Then** the system blocks removal until the loan is fully repaid or settled.

---

### User Story 3 — Monthly Contribution Cycle (Priority: P1)

Each month the system generates contribution dues for every active member. Members (or Fund Admin on their behalf) record payments. The system tracks payment status and maintains a fund ledger.

**Why this priority**: Contributions are the core economic activity — without them no pooled money exists for lending.

**Independent Test**: Can be tested by triggering a monthly cycle, verifying dues appear for each member, recording a payment, and checking the ledger.

**Acceptance Scenarios**:

1. **Given** an Active fund with 5 members, **When** the monthly contribution cycle runs (1st of each month), **Then** 5 ContributionDue records are created, each with the member's fixed amount and status Pending.
2. **Given** a Pending contribution due of INR 1,000, **When** the Fund Admin records a full payment of INR 1,000, **Then** the status changes to Paid and a Transaction record is created.
3. **Given** a Pending due of INR 1,000, **When** a partial payment of INR 600 is recorded, **Then** the status changes to Partial, the remaining balance shows INR 400, and a Transaction is created.
4. **Given** a contribution due still Pending after the due date, **When** the grace period expires, **Then** the status changes to Late and a reminder notification is sent.
5. **Given** a Late contribution that remains unpaid by month-end, **When** the next cycle runs, **Then** the previous month's due is marked Missed and the new month's due is created independently.

---

### User Story 4 — Member Requests a Loan (Priority: P1)

An active member requests a loan from the fund, specifying principal amount, desired start month, and optional purpose. The request follows the fund's configured approval workflow.

**Why this priority**: Lending is the core value proposition — it gives members access to pooled capital.

**Independent Test**: Can be tested by submitting a loan request and verifying it appears in the Fund Admin's approval queue with correct details.

**Acceptance Scenarios**:

1. **Given** a member in an Active fund with sufficient pooled balance, **When** they submit a loan request for INR 50,000 starting next month, **Then** a Loan record is created with status Pending Approval.
2. **Given** a fund configured with Admin-only approval, **When** the Fund Admin approves a Pending loan and sets a scheduled monthly installment (e.g., INR 2,000), **Then** the loan status changes to Approved with the scheduled installment recorded, a disbursement Transaction is created, and the member is notified.
3. **Given** a fund configured with Admin-only approval, **When** the Fund Admin rejects a loan with a reason, **Then** the loan status changes to Rejected and the member is notified with the reason.
4. **Given** a loan request exceeding the fund's max-loan-per-member cap, **When** submitted, **Then** the system rejects it with a clear error citing the cap.
5. **Given** a member who already has the maximum concurrent loans, **When** they submit another request, **Then** the system rejects it.

---

### User Story 5 — Loan Voting Workflow (Priority: P2)

For funds configured to require voting on loan requests, the Fund Admin triggers a vote. All Editors cast votes within a configurable window, and the admin finalises the result.

**Why this priority**: Voting is an optional overlay on the loan approval flow; the base approval works without it.

**Independent Test**: Can be tested by enabling voting on a fund, triggering a vote on a loan request, having members vote, and verifying tally + admin finalisation.

**Acceptance Scenarios**:

1. **Given** a fund with voting enabled, **When** the Fund Admin flags a loan request for voting, **Then** a VotingSession is created with a configurable window (24–72 hours) and all Editors are notified.
2. **Given** an open VotingSession, **When** an Editor casts an Approve or Reject vote, **Then** their vote is recorded and they cannot vote again on the same session.
3. **Given** a VotingSession whose window has expired, **When** the Fund Admin views results, **Then** they see vote counts (approve/reject/abstain) and whether the configured threshold is met.
4. **Given** vote results showing majority approval, **When** the Fund Admin finalises with Approve, **Then** the loan proceeds to disbursement.
5. **Given** vote results showing majority rejection, **When** the Fund Admin attempts to override and approve, **Then** the system allows it (Admin has final authority) but logs the override in the audit trail.

---

### User Story 6 — Loan Repayment on Reducing Balance (Priority: P1)

Each month the system calculates interest on the outstanding principal (reducing balance method) and a minimum principal portion. The borrower (or Admin) records repayments.

**Why this priority**: Repayment tracking is essential to fund economics — interest earnings fund the dissolution payout.

**Independent Test**: Can be tested with a known loan amount, verifying month-by-month interest and principal calculations match the formula.

**Acceptance Scenarios**:

1. **Given** a loan of INR 50,000 at 2% monthly interest, **When** the first month's repayment is generated, **Then** interest due = INR 1,000, principal due ≥ INR 1,000 (minimum), total due ≥ INR 2,000.
2. **Given** a repayment of INR 2,000 (INR 1,000 interest + INR 1,000 principal), **When** recorded, **Then** outstanding principal becomes INR 49,000 and a RepaymentEntry is created.
3. **Given** outstanding principal of INR 800 (below minimum principal of INR 1,000), **When** the final repayment is generated, **Then** principal due = INR 800 (capped to remaining), interest = INR 16, total = INR 816.
4. **Given** a repayment is overdue, **When** the due date passes without full payment, **Then** the repayment is marked Overdue and a reminder notification is sent.

---

### User Story 7 — Fund Dissolution & Settlement (Priority: P2)

When a fund is ready to close, the Fund Admin initiates dissolution. The system validates all conditions, calculates each member's net payout, and produces a settlement report.

**Why this priority**: Dissolution is the lifecycle end — important but occurs only once per fund.

**Independent Test**: Can be tested with a fund having known contributions and interest earnings, verifying that settlement calculations match expected payouts.

**Acceptance Scenarios**:

1. **Given** a Fund Admin of an Active fund, **When** they initiate dissolution, **Then** the fund transitions to Dissolving state, and no new loans or members are accepted.
2. **Given** a fund in Dissolving state, **When** the system runs settlement validation, **Then** it calculates gross payout, deducts outstanding loans and unpaid dues, and produces net payout per member.
3. **Given** a member whose net payout would be negative, **When** the validation runs, **Then** the system blocks dissolution with a clear report of which members have negative balances and by how much.
4. **Given** all net payouts are ≥ 0, **When** the Fund Admin confirms dissolution, **Then** the fund moves to Dissolved state and the settlement report is generated with a downloadable export.
5. **Given** interest pool of INR 10,000 and two members with contributions of INR 1,000 and INR 2,000 per month, **When** interest is shared, **Then** member 1 receives INR 3,333.33 and member 2 receives INR 6,666.67 (proportional to contribution weight).

---

### User Story 8 — Guest Views Fund Summary (Priority: P3)

A Guest user with read-only access can view fund summary, member list, contribution status, and reports without making any changes.

**Why this priority**: Read-only access is simple to implement and enables transparency for stakeholders.

**Independent Test**: Can be tested by logging in as Guest and verifying all views render data but no edit/create/delete actions are available.

**Acceptance Scenarios**:

1. **Given** a Guest user assigned to a fund, **When** they open the fund dashboard, **Then** they see fund summary (balance, member count, active loans) but no action buttons.
2. **Given** a Guest, **When** they navigate to any member's contribution history, **Then** they see payment statuses but cannot record or modify payments.
3. **Given** a Guest, **When** they attempt to access an admin-only screen via URL manipulation, **Then** the system returns an access-denied response.

---

### User Story 9 — Reports & Exports (Priority: P2)

Fund Admins and Editors can generate and export fund-level and member-level reports including contribution summaries, loan portfolios, interest earnings, and settlement reports.

**Why this priority**: Reporting provides transparency and is essential for auditing, but the core transactional flows take precedence.

**Independent Test**: Can be tested by generating a contribution summary report and verifying totals match ledger data.

**Acceptance Scenarios**:

1. **Given** a Fund Admin, **When** they request a monthly contribution report, **Then** the system generates a summary showing each member's payment status and fund total collected.
2. **Given** a Fund Admin, **When** they request a loan portfolio report, **Then** it shows all active loans with outstanding balances, interest accrued, and repayment status.
3. **Given** a member (Editor), **When** they request their personal statement, **Then** it shows their contribution history, loan history, and projected dissolution payout.
4. **Given** any report, **When** the user requests export, **Then** the system produces a downloadable PDF or CSV file.

---

### User Story 10 — Notifications & Reminders (Priority: P2)

The system sends timely notifications for key events: contribution dues, payment receipts, loan status changes, voting invitations, overdue reminders, and dissolution updates.

**Why this priority**: Notifications drive user engagement and timely payments but are an enhancement over manual tracking.

**Independent Test**: Can be tested by triggering a contribution cycle and verifying notification delivery to members.

**Acceptance Scenarios**:

1. **Given** a new contribution cycle, **When** dues are generated, **Then** each member receives a push notification and email with the amount due and deadline.
2. **Given** a payment is recorded, **When** the transaction is saved, **Then** the member receives a payment receipt notification.
3. **Given** a loan status change, **When** the status updates, **Then** the borrower receives a notification with the new status and any reason provided.
4. **Given** an overdue contribution or repayment, **When** the grace period expires, **Then** the member receives a reminder notification, repeated at configurable intervals.

---

### User Story 11 — Audit Trail & Security (Priority: P2)

Every state-changing action is logged with actor, timestamp, action, and before/after values. Platform enforces server-side RBAC. All data encrypted in transit and at rest.

**Why this priority**: Security and auditability are non-negotiable for financial systems but are infrastructure-level concerns layered onto functional flows.

**Independent Test**: Can be tested by performing an action (e.g., approving a loan) and verifying an audit log entry is created with correct details.

**Acceptance Scenarios**:

1. **Given** a Fund Admin approves a loan, **When** the action completes, **Then** an AuditLog entry is created with actor ID, timestamp, action type, entity reference, and before/after state.
2. **Given** an Editor attempts to remove a member, **When** the request reaches the server, **Then** it is rejected server-side (not just hidden in the UI) with an appropriate error.
3. **Given** any user session, **When** data is transmitted, **Then** all traffic is encrypted via TLS.

---

### Edge Cases

- **Double payment**: If the same payment is submitted twice (same idempotency key), the system processes it only once and returns the original result.
- **Concurrent payment recording**: If two users (e.g., Fund Admin and member) record a payment for the same due simultaneously with different idempotency keys, the system uses optimistic locking — the first write succeeds, the second receives a conflict error and the user must refresh the due's current state before retrying.
- **Partial payment followed by full payment**: When a partial is recorded and then the remainder arrives, the system correctly transitions status from Partial → Paid.
- **Member removal with outstanding contributions**: System blocks removal if the member has unpaid dues for the current cycle; all historical records are retained.
- **Role change mid-cycle**: If a member's role changes (e.g., Editor → Guest), their existing contribution dues and loans are unaffected; only future permissions change.
- **Dissolution with active loans**: During Dissolving state, active loans continue generating monthly repayment entries and borrowers repay normally. Dissolution confirmation is blocked unless all loans are Closed or the outstanding amounts are deductible from the member's payout without going negative.
- **Concurrent loan requests**: Two members submit loan requests simultaneously that would exceed the fund's available pool — the system processes requests sequentially and rejects the second if funds are insufficient.
- **Voting window expiry with no votes**: If no Editors vote within the window, the session result is "No quorum" and the Admin must decide without a recommendation.
- **Fund Admin resigns**: Platform Administrator must assign a new Fund Admin before removing the current one; a fund cannot exist without at least one Admin.
- **Contribution due generation failure**: If the monthly job fails partway, it must be idempotent — re-running it does not create duplicate dues.
- **Leap year / month-end handling**: Due dates always normalise to the last day of the month if the month has fewer days than the configured day (e.g., Feb 28/29).

---

## 3. Requirements

### Functional Requirements

#### Authentication & Profile Module

- **FR-001**: System MUST authenticate users via phone number or email OTP (one-time password).
- **FR-002**: System MUST issue a secure session token upon successful OTP verification.
- **FR-003**: Users MUST be able to view and edit their profile (name, phone, email, profile picture).
- **FR-004**: Users MUST see a list of all funds they belong to with their role in each fund.
- **FR-005**: System MUST support session expiry and forced logout.

#### Fund Management Module

- **FR-010**: Platform Administrators MUST be able to create new funds with the following configuration: fund name, description, currency (INR), monthly interest rate, minimum monthly contribution, minimum principal per repayment (default INR 1,000), loan approval policy (Admin-only or Admin+Voting), optional max loan per member, optional max concurrent loans per member, and dissolution policy.
- **FR-011**: Fund configuration fields (all except description) MUST be editable by Fund Admins while the fund is in Draft state. Once the fund is activated, all configuration fields except description become immutable. Description remains mutable in any state.
- **FR-012**: Every fund MUST have a lifecycle with states: Draft → Active → Dissolving → Dissolved.
- **FR-013**: Only Platform Administrators MUST be able to transition a fund from Draft to Active.
- **FR-014**: Platform Administrators MUST be able to assign or reassign Fund Admin role to any registered user.
- **FR-015**: System MUST enforce that every Active fund has at least one Fund Admin at all times.

#### Membership & Roles Module

- **FR-020**: Fund Admin MUST be able to invite users to join the fund by phone number or email.
- **FR-021**: Invited users MUST be able to accept or decline the invitation.
- **FR-022**: When accepting a fund invitation, a member MUST select a fixed monthly contribution amount that is greater than or equal to the fund's minimum contribution.
- **FR-023**: A member's monthly contribution amount MUST be immutable after joining.
- **FR-024**: Fund Admin MUST be able to remove members subject to restrictions (no outstanding loans, no unpaid dues for current cycle).
- **FR-025**: System MUST support four roles: Platform Administrator (platform-level), Admin (fund-level), Editor (fund member), and Guest (fund read-only).
- **FR-026**: A single user MUST be able to belong to multiple funds with different roles in each.
- **FR-027**: Role assignments MUST be managed at the fund level by the Fund Admin, except Platform Administrator which is managed at platform level.

#### Contributions Module

- **FR-030**: System MUST automatically generate monthly contribution dues for every active member on the 1st of each month (or configurable day).
- **FR-031**: Each ContributionDue MUST track: member, fund, month/year, amount, status, due date, and paid date.
- **FR-032**: Contribution statuses MUST include: Pending, Paid, Partial, Late, and Missed.
- **FR-033**: System MUST transition a Pending due to Late if not fully paid by the due date plus a configurable grace period (default: 5 days).
- **FR-034**: System MUST transition a Late due to Missed if not fully paid by the end of the month.
- **FR-035**: Fund Admin and Editor MUST be able to record contribution payments (manual entry), with an idempotency key to prevent duplicates.
- **FR-035a**: Payment recording MUST use optimistic concurrency control (version-based). If the ContributionDue or RepaymentEntry has been modified since the user last read it, the write MUST fail with a conflict error prompting the user to refresh and retry.
- **FR-036**: System MUST maintain a fund ledger of all contribution transactions with immutable entries.
- **FR-037**: Partial payments MUST be tracked with remaining balance and multiple transaction records.
- **FR-038**: System MUST generate a payment receipt for each recorded transaction.

#### Loans Module

- **FR-040**: Only active members (Editors) MUST be able to request loans from their fund.
- **FR-041**: Loan request MUST include: principal amount, requested start month, and optional purpose.
- **FR-042**: System MUST validate loan requests against fund policies: max loan per member, max concurrent loans, and available fund pool balance.
- **FR-043**: Default loan approval MUST be Fund Admin-only (approve/reject with reason). Upon approval, Fund Admin MUST set the `scheduled_installment` amount (monthly principal installment); defaults to INR 0 if not specified.
- **FR-043a**: The `scheduled_installment` MUST be immutable after loan approval.
- **FR-044**: Fund Admin MUST be able to optionally enable a voting workflow for a specific loan request.
- **FR-045**: When voting is enabled, all Editors in the fund MUST be eligible to vote (approve/reject).
- **FR-046**: Voting window MUST be configurable between 24 and 72 hours.
- **FR-047**: Voting threshold MUST be configurable: simple majority (default) or a custom percentage.
- **FR-048**: Fund Admin MUST see vote results before finalizing the outcome (approve/reject).
- **FR-049**: Fund Admin MUST retain final authority to override vote results; overrides are logged in the audit trail.
- **FR-050**: Upon approval, system MUST create a disbursement Transaction and update the fund pool balance.
- **FR-051**: Upon rejection, system MUST notify the borrower with the reason.

#### Repayment Module

- **FR-060**: System MUST calculate monthly repayment using reducing-balance method: `interest_due = outstanding_principal × monthly_interest_rate`.
- **FR-061**: Minimum principal due each month MUST be the greater of the fund's configured `minimum_principal` (default INR 1,000) or the `scheduled_installment`, capped at the remaining principal.
- **FR-062**: Total monthly repayment due MUST equal `interest_due + principal_due`.
- **FR-063**: After each repayment, outstanding principal MUST be reduced: `outstanding_new = outstanding_old − principal_paid`.
- **FR-064**: Interest earned on each repayment MUST be recorded as fund income in the ledger.
- **FR-065**: System MUST generate monthly repayment entries for each active loan.
- **FR-066**: System MUST track repayment statuses: Pending, Paid, Partial, Overdue.
- **FR-067**: Repayment recording MUST use an idempotency key to prevent duplicate entries.
- **FR-068**: If a repayment exceeds the total due for that month, the excess MUST be applied to principal reduction.

#### Overdue Handling

- **FR-070**: System MUST mark a repayment as Overdue if not fully paid by the due date.
- **FR-071**: System MUST send overdue reminders at configurable intervals (default: 3 days, 7 days, 14 days after due date).
- **FR-072**: Penalty on overdue repayments MUST default to none; Fund Admin may configure a flat or percentage penalty (applied to the overdue amount) at fund creation.
- **FR-073**: If a penalty is configured, it MUST be added to the next month's repayment entry as a separate line item.

#### Dissolution & Settlement Module

- **FR-080**: Fund Admin MUST be able to initiate dissolution, transitioning the fund from Active to Dissolving.
- **FR-081**: In Dissolving state, system MUST block new member joins, new loan requests, and new contributions.
- **FR-081a**: In Dissolving state, active loans MUST continue generating monthly repayment entries. Borrowers MUST continue normal repayments until each loan is Closed.
- **FR-082**: System MUST calculate each member's settlement as:
  - `gross_payout = total_paid_contributions + interest_share`
  - `net_payout = gross_payout − outstanding_loan_principal − unpaid_interest − unpaid_dues`
- **FR-083**: System MUST block dissolution if any member's `net_payout < 0`.
- **FR-084**: Interest sharing MUST be proportional to each member's fixed monthly contribution amount:
  - `member_weight = member_monthly_contribution_amount`
  - `member_interest_share = total_interest_pool × (member_weight / sum_of_all_weights)`
- **FR-085**: System MUST produce a settlement report with per-member breakdown: contributions paid, interest share, outstanding deductions, and net payout.
- **FR-086**: Settlement report MUST be exportable as PDF and CSV.
- **FR-087**: Upon final confirmation, the fund MUST transition to Dissolved and become read-only.
- **FR-088**: Interest sharing eligibility rule MUST be configurable (default: all active members weighted by contribution amount).

#### Reports Module

- **FR-090**: System MUST provide fund-level reports: monthly contribution summary, loan portfolio, interest earnings, fund balance sheet.
- **FR-091**: System MUST provide member-level reports: personal contribution history, personal loan history, projected dissolution payout.
- **FR-092**: All reports MUST support date-range filtering.
- **FR-093**: All reports MUST be exportable as PDF and CSV.

#### Notifications Module

- **FR-100**: System MUST send notifications via push (mobile), in-app, and email channels.
- **FR-101**: SMS notifications MUST be reserved for critical events only (overdue payments, loan disbursement).
- **FR-102**: Users MUST be able to configure notification preferences per channel.
- **FR-103**: All notifications MUST use templates with placeholder substitution (member name, amount, fund name, due date, etc.).
- **FR-104**: On delivery failure, system MUST retry the primary channel up to 3 times with exponential backoff (e.g., 30s, 2min, 10min). If all retries are exhausted, system MUST fall back to the next channel in priority order: push → email → in-app.
- **FR-105**: If all channels fail for a notification, the system MUST mark it as Failed, log the failure, and ensure the notification remains visible in the member's in-app notification centre.
- **FR-106**: System MUST use a provider abstraction layer (`ISmsSender`, `IEmailSender`, `IPushNotificationSender`) to decouple notification dispatch from vendor-specific implementations. Providers MUST be swappable via dependency injection.
- **FR-107**: System MUST resolve a recipient's contact information (email, phone, device tokens) from the Identity service before attempting channel delivery. If contact info is unavailable for a channel, the system MUST fall back to the next available channel.
- **FR-108**: OTP codes MUST be delivered via the Notifications service using the `OtpRequested` integration event. The Identity service MUST NOT return the plaintext OTP in the API response body.
- **FR-109**: For local development, the system MUST provide mock notification providers: console logger for SMS and push, MailHog SMTP capture for email. Real provider credentials MUST NOT be required for development.

#### Audit & Security Module

- **FR-110**: System MUST log every state-changing action as an AuditLog entry with: actor, timestamp, action type, entity type, entity ID, before-state, after-state.
- **FR-111**: System MUST enforce RBAC server-side on every endpoint (not UI-only).
- **FR-112**: All data in transit MUST be encrypted via TLS 1.2+.
- **FR-113**: All data at rest MUST be encrypted.
- **FR-114**: System MUST support API-level idempotency for payment posting and repayment posting via a client-generated idempotency key header.
- **FR-115**: System MUST prevent escalation of privileges — a user cannot assign themselves a higher role.

---

## 4. Role / Permission Matrix

### Platform-Level Roles

| Action                         | Platform Administrator | Fund Admin | Editor | Guest |
| ------------------------------ | ---------------------- | ---------- | ------ | ----- |
| Create fund                    | Yes                    | No         | No     | No    |
| Delete/archive fund            | Yes                    | No         | No     | No    |
| Assign Fund Admin              | Yes                    | No         | No     | No    |
| View all funds                 | Yes                    | No         | No     | No    |
| Manage platform settings       | Yes                    | No         | No     | No    |
| Transition fund Draft → Active | Yes                    | No         | No     | No    |

### Fund-Level Roles

| Action                            | Fund Admin | Editor | Guest |
| --------------------------------- | ---------- | ------ | ----- |
| Edit fund description             | Yes        | No     | No    |
| Edit fund configuration (Draft)   | Yes        | No     | No    |
| Invite/remove members             | Yes        | No     | No    |
| Assign/change member roles        | Yes        | No     | No    |
| Record contribution payments      | Yes        | Yes    | No    |
| View contribution status          | Yes        | Yes    | Yes   |
| Request a loan                    | No*        | Yes    | No    |
| Approve/reject loan               | Yes        | No     | No    |
| Trigger voting on a loan          | Yes        | No     | No    |
| Cast vote on a loan               | No         | Yes    | No    |
| Record repayments                 | Yes        | Yes    | No    |
| View loan portfolio               | Yes        | Yes    | Yes   |
| Initiate dissolution              | Yes        | No     | No    |
| Confirm dissolution               | Yes        | No     | No    |
| View reports                      | Yes        | Yes    | Yes   |
| Export reports                     | Yes        | Yes    | No    |
| View audit logs                   | Yes        | No     | No    |
| View fund dashboard               | Yes        | Yes    | Yes   |
| Configure overdue penalties       | Yes        | No     | No    |

*Fund Admin can also be a contributing member (Editor role is implicit for Admin regarding contributions). If Fund Admin wishes to take a loan, a separate Fund Admin must approve it, or Platform Administrator can approve.

---

## 5. User Stories by Role

### Platform Administrator Stories

- As a Platform Administrator, I want to create a new fund with all configuration parameters so that community groups can start managing their money.
- As a Platform Administrator, I want to assign a Fund Admin to a newly created fund so that fund operations can begin.
- As a Platform Administrator, I want to view all funds on the platform with their statuses so I can monitor platform health.
- As a Platform Administrator, I want to deactivate or archive a dissolved fund so that platform data remains manageable.
- As a Platform Administrator, I want to manage platform-wide settings (notification defaults, supported channels) to ensure consistent operations.

### Fund Admin Stories

- As a Fund Admin, I want to invite members to my fund so that they can contribute and participate.
- As a Fund Admin, I want to remove inactive members (with no outstanding obligations) so that the fund roster stays current.
- As a Fund Admin, I want to record contribution payments on behalf of members so that the fund ledger is accurate.
- As a Fund Admin, I want to review and approve/reject loan requests so that lending is controlled and fair.
- As a Fund Admin, I want to optionally trigger a vote on a loan request so that the community has input on large loans.
- As a Fund Admin, I want to view the full fund ledger and audit logs so that I can ensure transparency.
- As a Fund Admin, I want to initiate and confirm fund dissolution so that members receive their fair payouts.
- As a Fund Admin, I want to generate and export reports so that fund members have documentation.
- As a Fund Admin, I want to configure overdue penalty rules so that the fund's policies are enforced.
- As a Fund Admin, I want to edit fund configuration fields (name, interest rate, contribution settings, loan policy, etc.) while the fund is in Draft state, so that I can fine-tune settings before activation.

### Editor (Member) Stories

- As an Editor, I want to view my monthly contribution dues so that I know how much I owe.
- As an Editor, I want to record my own contribution payment so that my dues are marked as paid.
- As an Editor, I want to request a loan from the fund so that I can access pooled capital when needed.
- As an Editor, I want to view my loan repayment schedule so that I can plan my monthly payments.
- As an Editor, I want to record my loan repayment so that my outstanding balance is reduced.
- As an Editor, I want to cast a vote on a loan request (when voting is enabled) so that I can participate in fund governance.
- As an Editor, I want to view my personal statement (contributions, loans, projected payout) so that I understand my financial position.
- As an Editor, I want to receive notifications for dues, payments, loan status changes, and voting invitations so that I stay informed.

### Guest Stories

- As a Guest, I want to view the fund dashboard summary so that I can understand the fund's health.
- As a Guest, I want to view member contribution statuses so that I can see who has paid.
- As a Guest, I want to view the loan portfolio so that I can see the fund's lending activity.
- As a Guest, I want to view reports so that I can audit the fund's operations.

---

## 6. Workflows

### 6.1 Contribution Cycle Workflow

1. **Trigger**: System scheduled job runs on the 1st of each month (or configurable day).
2. **Generate dues**: For each active member in each Active fund, create a ContributionDue record with the member's fixed amount, status = Pending, and due date = configured day of month + grace period.
3. **Notify**: Send contribution-due notification to each member.
4. **Payment recording**: Fund Admin or Editor records payment → creates Transaction → updates ContributionDue status.
5. **Partial flow**: If amount < due, status → Partial, remaining balance tracked.
6. **Late detection**: After due date + grace period, any Pending/Partial due → Late; send reminder.
7. **Missed detection**: At month-end, any Late/Pending due → Missed.
8. **Ledger**: Every Transaction is appended to the immutable fund ledger.

### 6.2 Loan Request & Approval Workflow

1. **Request**: Editor submits loan request (principal, start month, purpose).
2. **Validation**: System checks against fund pool balance, max loan cap, max concurrent loans.
3. **Route**:
   - If Admin-only approval: route to Fund Admin's approval queue.
   - If Admin selects voting: proceed to Voting Workflow (6.3).
4. **Admin decision**: Fund Admin approves or rejects (with reason). On approval, Admin sets the `scheduled_installment` (monthly principal installment amount; defaults to INR 0 if not specified). This value is immutable after approval.
5. **Approval → Disbursement**: System creates a disbursement Transaction, deducting from fund pool and recording as loan to member. Loan status → Active. The scheduled installment is stored on the Loan record.
6. **Rejection**: Notification sent to borrower with reason. Loan status → Rejected.

### 6.3 Voting Workflow

1. **Trigger**: Fund Admin flags a loan request for voting.
2. **Create session**: VotingSession created with voting window (24–72h configurable).
3. **Notify voters**: All Editors in the fund receive a notification with loan details and voting link.
4. **Voting**: Each Editor casts one vote (Approve/Reject). Votes are immutable once cast.
5. **Window close**: After the window expires, votes are tallied.
6. **Result**: System checks against threshold (simple majority or configured percentage).
7. **Admin review**: Fund Admin sees results and finalises: approve, reject, or override. Override is logged in audit trail.

### 6.4 Repayment Workflow

1. **Monthly generation**: For each Active loan, generate a RepaymentEntry with:
   - `interest_due = outstanding_principal × monthly_interest_rate`
   - `principal_due = max(INR 1,000, scheduled_installment)` capped at remaining principal
   - `total_due = interest_due + principal_due`
2. **Notify**: Send repayment-due notification to borrower.
3. **Payment recording**: Borrower or Admin records repayment → Transaction created.
4. **Apply payment**: Interest portion applied first, then principal. Excess applied to additional principal reduction.
5. **Update principal**: `outstanding_new = outstanding_old − principal_paid`.
6. **Record interest**: Interest portion recorded as fund income.
7. **Loan closure**: When outstanding principal reaches 0, loan status → Closed.
8. **Overdue**: If not paid by due date, status → Overdue; send reminders per schedule.

### 6.5 Dissolution Wizard Workflow

1. **Initiate**: Fund Admin clicks "Initiate Dissolution" → fund state → Dissolving.
2. **Block new activity**: No new members, loans, or contributions accepted.
3. **Continue loan repayments**: Active loans continue generating monthly RepaymentEntries. Borrowers repay normally until loans are Closed or outstanding balances are deductible from their settlement payout without going negative.
4. **Outstanding settlement**: System calculates each member's outstanding loans, unpaid interest, and unpaid dues.
4. **Interest pool**: Sum all interest earned across all loans.
5. **Calculate settlement per member**:
   - `gross_payout = total_paid_contributions + interest_share`
   - `interest_share = total_interest_pool × (member_weight / total_weight)`
   - `net_payout = gross_payout − outstanding_loan_principal − unpaid_interest − unpaid_dues`
6. **Validation**: If any `net_payout < 0`, block dissolution with details.
7. **Review**: Fund Admin and members review the settlement report.
8. **Confirm**: Fund Admin confirms → fund state → Dissolved (read-only).
9. **Export**: Settlement report generated as PDF/CSV.

---

## 7. Calculation Rules, Formulas & Examples

### 7.1 Reducing-Balance Interest Formula

```
interest_due = outstanding_principal × monthly_interest_rate
principal_due = max(minimum_principal, scheduled_installment), capped at outstanding_principal
total_due = interest_due + principal_due
outstanding_new = outstanding_principal − principal_paid
```

Where:
- `monthly_interest_rate` = the rate configured on the fund (e.g., 0.02 for 2%)
- `minimum_principal` = the fund-level configured minimum principal per repayment; default INR 1,000. Editable while the fund is in Draft state; immutable once activated (per FR-011). Stored on the Fund entity.
- `scheduled_installment` = the monthly principal installment set by the Fund Admin at loan approval time; defaults to INR 0 if not specified. Immutable after approval. Stored on the Loan entity.

### 7.2 Worked Example

**Loan**: INR 50,000 | **Monthly interest rate**: 2% | **Minimum principal**: INR 1,000

| Month | Outstanding (Start) | Interest Due (2%) | Principal Due | Total Due | Outstanding (End) |
| ----- | ------------------- | ------------------ | ------------- | --------- | ----------------- |
| 1     | 50,000.00           | 1,000.00           | 1,000.00      | 2,000.00  | 49,000.00         |
| 2     | 49,000.00           | 980.00             | 1,000.00      | 1,980.00  | 48,000.00         |
| 3     | 48,000.00           | 960.00             | 1,000.00      | 1,960.00  | 47,000.00         |
| ...   | ...                 | ...                | ...           | ...       | ...               |
| 49    | 2,000.00            | 40.00              | 1,000.00      | 1,040.00  | 1,000.00          |
| 50    | 1,000.00            | 20.00              | 1,000.00      | 1,020.00  | 0.00              |

**Final month scenario** (remaining < INR 1,000):

| Month | Outstanding (Start) | Interest Due (2%) | Principal Due (capped) | Total Due | Outstanding (End) |
| ----- | ------------------- | ------------------ | ---------------------- | --------- | ----------------- |
| N     | 800.00              | 16.00              | 800.00                 | 816.00    | 0.00              |

### 7.3 Dissolution Settlement Formula

```
gross_payout = total_paid_contributions + interest_share
interest_share = total_interest_pool × (member_weight / total_weight)
member_weight = member_monthly_contribution_amount
net_payout = gross_payout − outstanding_loan_principal − unpaid_interest − unpaid_dues
```

**Example**: Fund with 3 members, total interest pool = INR 30,000

| Member | Monthly Contribution | Weight | Interest Share | Contributions Paid | Gross Payout | Outstanding Loan | Net Payout |
| ------ | -------------------- | ------ | -------------- | ------------------ | ------------ | ---------------- | ---------- |
| A      | 1,000                | 1,000  | 5,000.00       | 12,000.00          | 17,000.00    | 0.00             | 17,000.00  |
| B      | 2,000                | 2,000  | 10,000.00      | 24,000.00          | 34,000.00    | 5,000.00         | 29,000.00  |
| C      | 3,000                | 3,000  | 15,000.00      | 36,000.00          | 51,000.00    | 0.00             | 51,000.00  |
| **Total** | —               | 6,000  | 30,000.00      | 72,000.00          | 102,000.00   | 5,000.00         | 97,000.00  |

Interest share calculation: A = 30,000 × (1,000/6,000) = 5,000; B = 30,000 × (2,000/6,000) = 10,000; C = 30,000 × (3,000/6,000) = 15,000.

### 7.4 Rounding Rules

- All monetary values rounded to 2 decimal places (paisa precision).
- Rounding method: Banker's rounding (round half to even) to minimise cumulative bias.
- When distributing interest pool, any rounding remainder (typically ≤ INR 0.01) is added to the member with the highest weight.

### 7.5 Edge Cases in Calculations

- **Zero outstanding principal**: No interest or repayment generated; loan is Closed.
- **Principal due exceeds outstanding**: Cap principal due to remaining outstanding.
- **Overpayment on repayment**: Excess applied to principal reduction; new outstanding recalculated.
- **No contributions paid (new member immediately in dissolution)**: gross_payout = 0 + interest_share; net_payout may be negative if they have a loan → dissolution blocked.
- **Single member in fund**: That member gets 100% of the interest pool.

---

## 8. Data Model

### Key Entities

- **User**: Represents an individual on the platform. Key attributes: unique ID, name, phone, email, profile picture, authentication tokens, created/updated timestamps.
- **Fund**: Represents a community fund. Key attributes: unique ID, name, description, currency (INR), monthly interest rate, minimum monthly contribution, minimum principal per repayment (default INR 1,000), loan approval policy (Admin-only / Admin+Voting), max loan per member (optional), max concurrent loans (optional), dissolution policy, state (Draft/Active/Dissolving/Dissolved), created/updated timestamps.
- **FundRoleAssignment**: Maps a user to a fund with a specific role. Key attributes: unique ID, user reference, fund reference, role (Admin/Editor/Guest), assigned date, assigned by.
- **MemberContributionPlan**: Records a member's fixed monthly contribution amount for a fund. Key attributes: unique ID, user reference, fund reference, monthly contribution amount, join date.
- **ContributionDue**: A monthly obligation for a member. Key attributes: unique ID, member contribution plan reference, fund reference, month/year, amount due, amount paid, remaining balance, status (Pending/Paid/Partial/Late/Missed), due date, paid date, created/updated timestamps.
- **Transaction**: An immutable ledger entry for any money movement. Key attributes: unique ID, fund reference, user reference, type (Contribution/Disbursement/Repayment/InterestIncome/Penalty/Settlement), amount, idempotency key, reference entity (ContributionDue or RepaymentEntry), created timestamp, recorded by.
- **Loan**: A lending record. Key attributes: unique ID, fund reference, borrower (user reference), principal amount, outstanding principal, monthly interest rate (snapshot from fund), scheduled installment (set by Fund Admin at approval; default INR 0; immutable after approval), requested start month, purpose, status (PendingApproval/Approved/Active/Closed/Rejected), approved by, approval date, disbursement date, created/updated timestamps.
- **RepaymentEntry**: A monthly repayment obligation for a loan. Key attributes: unique ID, loan reference, month/year, interest due, principal due, total due, amount paid, status (Pending/Paid/Partial/Overdue), due date, paid date, created/updated timestamps.
- **VotingSession**: A voting event for a loan request. Key attributes: unique ID, loan reference, fund reference, voting window start, voting window end, threshold type (majority/percentage), threshold value, result (Pending/Approved/Rejected/NoQuorum), finalised by, finalised date.
- **Vote**: An individual vote in a session. Key attributes: unique ID, voting session reference, voter (user reference), decision (Approve/Reject), cast timestamp.
- **DissolutionSettlement**: A fund-level dissolution record. Key attributes: unique ID, fund reference, total interest pool, total contributions collected, settlement date, status (Calculating/Reviewed/Confirmed), confirmed by.
- **DissolutionLineItem**: Per-member settlement detail. Key attributes: unique ID, settlement reference, user reference, total paid contributions, interest share, outstanding loan principal, unpaid interest, unpaid dues, gross payout, net payout.
- **AuditLog**: Immutable log of state changes. Key attributes: unique ID, actor (user reference), timestamp, action type, entity type, entity ID, before-state (JSON), after-state (JSON), IP address, user agent.
- **Notification**: A notification record. Key attributes: unique ID, recipient (user reference), fund reference, channel (push/email/SMS/in-app), template key, placeholders (JSON), status (Pending/Sent/Failed), scheduled at, sent at.

### Relationships

- User ↔ Fund: many-to-many through FundRoleAssignment.
- User ↔ Fund: many-to-many through MemberContributionPlan (one plan per user per fund).
- Fund → ContributionDue: one-to-many.
- MemberContributionPlan → ContributionDue: one-to-many.
- Fund → Loan: one-to-many.
- Loan → RepaymentEntry: one-to-many.
- Loan → VotingSession: one-to-one (optional).
- VotingSession → Vote: one-to-many.
- Fund → DissolutionSettlement: one-to-one.
- DissolutionSettlement → DissolutionLineItem: one-to-many.
- Transaction references either ContributionDue or RepaymentEntry (polymorphic reference).

### Constraints

- Unique constraint: one MemberContributionPlan per (user, fund) pair.
- Unique constraint: one Vote per (voting session, voter) pair.
- Unique constraint: idempotency key per Transaction (fund-scoped).
- ContributionDue.amount_due must equal MemberContributionPlan.amount for that member.
- Loan.principal_amount must be > 0 and ≤ fund's max loan per member (if configured).
- Fund.monthly_interest_rate must be > 0 and ≤ 100 (percentage).
- MemberContributionPlan.amount ≥ Fund.minimum_monthly_contribution.

### Suggested Indexes

- FundRoleAssignment: (user_id, fund_id), (fund_id, role).
- ContributionDue: (fund_id, month_year), (user_id, fund_id, month_year).
- Transaction: (fund_id, created_at), (idempotency_key) unique.
- Loan: (fund_id, status), (borrower_id, fund_id).
- RepaymentEntry: (loan_id, month_year).
- AuditLog: (entity_type, entity_id), (actor_id, timestamp).
- VotingSession: (loan_id).

---

## 9. UI Screen List

### Web Application (Admin-Heavy)

| Screen                       | Core Components                                                                            |
| ---------------------------- | ------------------------------------------------------------------------------------------ |
| **Login / OTP Verification** | Phone/email input, OTP input, resend OTP button, error messages                            |
| **Platform Dashboard**       | Fund count, active/dissolved stats, quick-create fund button, fund list table with filters |
| **Fund Creation Form**       | Fund name, description, interest rate, min contribution, min principal per repayment (default INR 1,000), loan policy toggles, caps, submit |
| **Fund Detail Dashboard**    | Fund summary cards (balance, members, active loans), state badge, quick actions            |
| **Member Management**        | Member list table (name, role, contribution, status), invite button, remove button, role dropdown |
| **Contribution Dashboard**   | Month selector, member-wise due status grid, record payment modal, bulk status view        |
| **Contribution Ledger**      | Transaction table with filters (date, member, type), export buttons                       |
| **Loan Management**          | Loan requests queue, active loans table, loan detail panel with approve/reject/vote actions |
| **Loan Detail**              | Loan summary, repayment schedule table, repayment recording form, voting results panel     |
| **Voting Panel**             | Loan details, vote tally chart, voter list, finalise buttons, override warning             |
| **Dissolution Wizard**       | Step 1: Initiate, Step 2: Review settlements table, Step 3: Resolve blockers, Step 4: Confirm |
| **Settlement Report**        | Per-member breakdown table, totals row, export PDF/CSV buttons                            |
| **Reports Hub**              | Report type selector, date range picker, generate button, results table/chart, export      |
| **Audit Log Viewer**         | Filterable log table (date, actor, action, entity), detail expansion panel                |
| **Fund Settings**            | Config fields editable in Draft state (read-only after activation), description always editable, penalty configuration |
| **User Profile**             | Name, phone, email, profile picture, fund membership list                                 |
| **Notification Preferences** | Per-channel toggle (push, email, SMS), per-event-type toggles                             |

### Mobile Application (Member-Heavy)

| Screen                      | Core Components                                                                    |
| --------------------------- | ---------------------------------------------------------------------------------- |
| **Login / OTP**             | Phone/email input, OTP input, biometric option                                     |
| **Home / Fund List**        | Fund cards with role badge, balance preview, quick actions                          |
| **Fund Dashboard (Member)** | Balance summary, next due amount + date, active loans count, quick pay button      |
| **My Contributions**        | Month-by-month list with status badges, payment recording button, receipt download  |
| **Pay Contribution**        | Amount display (pre-filled), payment method selector, confirm button               |
| **My Loans**                | Active/closed loans list, outstanding balance, next repayment info                 |
| **Loan Request Form**       | Principal input, start month picker, purpose text field, submit button             |
| **Loan Detail (Member)**    | Repayment schedule list, pay button, interest/principal breakdown per month        |
| **Pay Repayment**           | Amount display (pre-filled), payment method selector, confirm button               |
| **Voting Screen**           | Loan request summary, approve/reject buttons, vote status indicator                |
| **My Statement**            | Contributions total, loans total, projected payout, downloadable PDF               |
| **Notifications**           | Notification list with type icons, read/unread states, tap to navigate             |
| **Profile & Settings**      | Name, phone, email edit, notification preferences, fund list                       |
| **Fund Members**            | Member list with roles and contribution amounts                                     |
| **Settlement Preview**      | Personal settlement breakdown (visible during Dissolving state)                     |

---

## 10. Notification Rules

| Event                        | Recipients          | Channels           | Template Placeholders                                          | Schedule              |
| ---------------------------- | ------------------- | ------------------- | -------------------------------------------------------------- | --------------------- |
| Contribution due generated   | Member              | Push, Email, In-app | `{member_name}`, `{fund_name}`, `{amount}`, `{due_date}`      | On generation (1st)   |
| Contribution payment received | Member             | Push, In-app        | `{member_name}`, `{fund_name}`, `{amount}`, `{paid_date}`     | Immediate             |
| Contribution overdue         | Member              | Push, Email, SMS    | `{member_name}`, `{fund_name}`, `{amount}`, `{days_overdue}`  | 3, 7, 14 days overdue |
| Loan request submitted       | Fund Admin          | Push, Email, In-app | `{borrower_name}`, `{fund_name}`, `{principal}`, `{purpose}`  | Immediate             |
| Loan approved                | Borrower            | Push, Email, In-app | `{fund_name}`, `{principal}`, `{start_month}`                  | Immediate             |
| Loan rejected                | Borrower            | Push, Email, In-app | `{fund_name}`, `{principal}`, `{reason}`                       | Immediate             |
| Loan disbursed               | Borrower            | Push, Email, SMS    | `{fund_name}`, `{principal}`, `{disbursement_date}`            | Immediate             |
| Voting session opened        | All Editors         | Push, Email, In-app | `{fund_name}`, `{borrower_name}`, `{principal}`, `{deadline}` | Immediate             |
| Voting reminder              | Non-voted Editors   | Push, In-app        | `{fund_name}`, `{hours_remaining}`                              | 12h before deadline   |
| Voting session closed        | Fund Admin          | Push, Email, In-app | `{fund_name}`, `{result}`, `{approve_count}`, `{reject_count}` | Immediate            |
| Repayment due generated      | Borrower            | Push, Email, In-app | `{fund_name}`, `{total_due}`, `{interest}`, `{principal}`      | On generation         |
| Repayment received           | Borrower            | Push, In-app        | `{fund_name}`, `{amount_paid}`, `{outstanding}`                | Immediate             |
| Repayment overdue            | Borrower            | Push, Email, SMS    | `{fund_name}`, `{total_due}`, `{days_overdue}`                 | 3, 7, 14 days overdue |
| Loan fully repaid            | Borrower, Fund Admin | Push, Email, In-app | `{fund_name}`, `{borrower_name}`, `{total_repaid}`            | Immediate             |
| Member invited               | Invitee             | Email, SMS          | `{fund_name}`, `{inviter_name}`, `{invite_link}`               | Immediate             |
| Member removed               | Removed member      | Push, Email         | `{fund_name}`, `{reason}`                                      | Immediate             |
| Dissolution initiated        | All members         | Push, Email, In-app | `{fund_name}`, `{initiated_by}`                                | Immediate             |
| Settlement report ready      | All members         | Push, Email, In-app | `{fund_name}`, `{net_payout}`, `{report_link}`                 | Immediate             |
| Fund dissolved               | All members         | Push, Email, In-app | `{fund_name}`, `{dissolution_date}`                            | Immediate             |

---

## 11. Audit Log Requirements

### Events to Log

| Event Category  | Specific Actions                                                                                      |
| --------------- | ----------------------------------------------------------------------------------------------------- |
| Authentication  | Login success, login failure, logout, OTP request, session expiry                                     |
| Fund Management | Fund created, fund state changed, fund config modified (description in any state; config fields in Draft state) |
| Membership      | Member invited, invitation accepted/declined, member removed, role assigned/changed                   |
| Contributions   | Contribution due generated, payment recorded, status changed (Pending→Paid, etc.)                     |
| Loans           | Loan requested, loan approved, loan rejected, loan disbursed, loan closed                             |
| Voting          | Voting session created, vote cast, session closed, admin override                                     |
| Repayments      | Repayment entry generated, repayment recorded, status changed, overpayment applied                    |
| Dissolution     | Dissolution initiated, settlement calculated, dissolution blocked (with reason), dissolution confirmed |
| Security        | Permission denied attempt, role escalation attempt, idempotency key collision                          |

### Audit Log Fields

- **Actor**: User ID of the person performing the action.
- **Timestamp**: UTC ISO-8601 timestamp.
- **Action Type**: Enum (e.g., FUND_CREATED, LOAN_APPROVED, PAYMENT_RECORDED).
- **Entity Type**: What was affected (Fund, Loan, ContributionDue, etc.).
- **Entity ID**: The unique identifier of the affected record.
- **Before State**: JSON snapshot of the entity before the change (null for creates).
- **After State**: JSON snapshot of the entity after the change (null for deletes).
- **IP Address**: Requester's IP address.
- **User Agent**: Requester's device/browser information.
- **Metadata**: Additional context (e.g., rejection reason, override justification).

### Retention

- Audit logs retained for 7 years after fund dissolution (aligned with data retention assumption).
- Audit logs are immutable — no edits or deletes allowed.

---

## 12. Validations & Edge Cases

### Double Payments

- Every payment/repayment endpoint requires a client-generated idempotency key (UUID).
- If the same idempotency key is submitted within a fund scope, the system returns the original response without creating a duplicate Transaction.
- Idempotency keys are stored and checked for 90 days.

### Partial Payments

- Partial contribution: status → Partial, remaining balance tracked. Subsequent payment appends a new Transaction and updates remaining balance. When fully paid, status → Paid.
- Partial repayment: applied to interest first, then principal. If insufficient to cover interest, the remaining interest carries forward. Status remains Partial until total due is fully paid.

### Member Removal

- Blocked if: member has outstanding loans (any status other than Closed) OR unpaid contribution dues for the current cycle.
- Upon valid removal: member's role assignment is deactivated (soft delete), historical data retained, no future dues generated.
- If member has a positive net balance (contributions paid, no loans), the balance is noted in the member's record for manual settlement by the Fund Admin.

### Role Changes

- Role changes take effect immediately for permission checks.
- Existing obligations (contribution dues, active loans, pending votes) are unaffected by role changes.
- Demoting an Admin to Editor requires at least one other Admin to remain in the fund.
- Promoting to Admin requires Platform Administrator or existing Fund Admin action.

### Dissolution Blocking

- Dissolution is blocked if any member's `net_payout < 0` after the formula: `gross_payout − outstanding_loan_principal − unpaid_interest − unpaid_dues`.
- The system presents a detailed report showing which members block dissolution and by how much.
- Fund Admin can request members to repay loans or cover dues before re-attempting dissolution.
- Fund cannot skip from Active to Dissolved — must go through Dissolving.

### Concurrent Operations

- Two simultaneous loan approvals that would deplete the fund pool: sequential processing with balance check before each disbursement. Second request rejected if balance is insufficient.
- Two simultaneous payment recordings for the same ContributionDue: idempotency key prevents duplication. If different keys but same due, the second is processed as an additional payment (updating the remaining balance or flagging overpayment).

### Month-End / Calendar Edge Cases

- Due dates normalised to last day of month if configured day doesn't exist (e.g., 31st → Feb 28/29).
- Leap year handling: February due date adjusts automatically.
- If a member joins mid-month, their first contribution due is generated for the next full month.

---

## 13. Non-Functional Requirements

### Security

- **NFR-001**: All endpoints MUST enforce server-side RBAC. UI-only hiding is not sufficient.
- **NFR-002**: All data in transit MUST be encrypted via TLS 1.2 or higher.
- **NFR-003**: All data at rest MUST be encrypted using industry-standard encryption.
- **NFR-004**: OTP codes MUST expire within 5 minutes and be single-use.
- **NFR-005**: Session tokens MUST expire after 24 hours of inactivity.
- **NFR-006**: Rate limiting MUST be applied to OTP requests (max 5 per phone/email per 15 minutes).
- **NFR-007**: All financial calculations MUST be performed server-side; client inputs are treated as untrusted.

### Reliability & Idempotency

- **NFR-010**: Payment posting and repayment posting endpoints MUST accept an `Idempotency-Key` header. Replayed requests with the same key MUST return the original response without side effects.
- **NFR-011**: Monthly dues generation and repayment generation jobs MUST be idempotent — re-running them for the same month does not create duplicates.
- **NFR-012**: System MUST handle partial failures in batch operations (e.g., dues generation for 1,000 members) by processing successfully for completed records and retrying failures.
- **NFR-013**: All state transitions MUST be atomic — no partial state changes on failure.

### Performance

- **NFR-020**: System MUST support up to 1,000 members per fund without degradation.
- **NFR-021**: System MUST support 100+ concurrent active funds.
- **NFR-022**: Dashboard and list screens MUST load in under 3 seconds on standard connections.
- **NFR-023**: Report generation for a fund with 1,000 members and 12 months of data MUST complete in under 30 seconds.
- **NFR-024**: Notification delivery MUST complete within 60 seconds of the triggering event.
- **NFR-025**: System MUST target 99.5% availability (~43.8 hours/year planned + unplanned downtime). Single-region deployment is acceptable for MVP.

### Privacy & Data Export

- **NFR-030**: Members MUST be able to export their personal data (contributions, loans, statements) on request.
- **NFR-031**: Fund data MUST be retained for 7 years after dissolution.
- **NFR-032**: Personal data deletion requests MUST be honoured in accordance with applicable privacy laws, with financial records anonymised rather than deleted.

### Observability

- **NFR-040**: System MUST provide structured logging for all operations (request ID, user ID, action, result).
- **NFR-041**: System MUST expose health-check endpoints for all services.
- **NFR-042**: System MUST track and alert on: error rates, response times, job failures (dues generation, notifications).
- **NFR-043**: System MUST support distributed tracing for cross-service operations.

### Backup & Recovery

- **NFR-050**: System MUST perform automated daily backups of all data.
- **NFR-051**: System MUST support point-in-time recovery within a 30-day window.
- **NFR-052**: Recovery time objective (RTO): 4 hours. Recovery point objective (RPO): 1 hour.

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: Fund Admins can create and configure a new fund in under 5 minutes.
- **SC-002**: Monthly contribution dues for a fund with 1,000 members are generated in under 60 seconds.
- **SC-003**: Members can view their dues and record a payment in under 2 minutes (mobile).
- **SC-004**: Loan requests are submitted in under 3 minutes by a member.
- **SC-005**: Loan approval/rejection completes within 2 interactions by the Fund Admin.
- **SC-006**: Voting sessions achieve at least 50% participation rate from eligible Editors (measured across all funds).
- **SC-007**: Settlement calculations for a fund with 100 members complete in under 10 seconds.
- **SC-008**: 95% of notifications are delivered within 60 seconds of the triggering event.
- **SC-009**: System supports 100 concurrent active funds with 1,000 members each without performance degradation.
- **SC-010**: Zero duplicate transactions in production, ensured by idempotency enforcement.
- **SC-011**: All financial calculations match expected values within INR 0.01 (rounding tolerance).
- **SC-012**: 90% of members can complete their first contribution payment without external help.
