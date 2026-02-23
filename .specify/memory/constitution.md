# FundManager Constitution

## Core Principles

### I. Multi-Fund Segregation Is Mandatory
Every fund is an independent ledger domain. Contributions, loans, repayments, charges, and distributions must always be scoped by `FundId`.
Cross-fund balance mixing is prohibited unless an explicit transfer workflow exists with two-sided accounting entries and audit trail.
Any query, API, or report that omits fund scoping is a defect.

### II. Financial Calculations Must Be Deterministic and Explainable
Monthly borrower interest is computed on reducing balance only. Calculation inputs (principal outstanding, annual/monthly rate, period dates, payment allocation) must be stored and reproducible.
Money amounts use decimal arithmetic only; floating-point arithmetic is forbidden for monetary values.
Rounding rules are explicit and consistent across web, mobile, and backend (default: round to 2 decimals, midpoint away from zero).

### III. Complete Auditability and Traceability
All financial events are immutable journal entries with actor, timestamp, source, and reason.
Corrections are made via reversal/adjustment entries; destructive updates to financial history are prohibited.
Every member statement and fund statement must be reconstructable from ledger data.

### IV. Fairness, Transparency, and Dissolution Correctness
Contribution obligations, lending eligibility, repayment schedules, and penalties (if any) must be rule-driven and visible to members.
At dissolution, distributable interest earnings are calculated from realized net interest (interest received minus approved loss/write-off impacts and approved expenses).
Distribution formula must be documented per fund and applied consistently; generated reports must show calculation basis per member.

### V. Security and Privacy by Default
Role-based access control is required (`Admin`, `Treasurer`, `Member`, optional `Auditor`).
Members can access only their personal data and fund-allowed aggregate views.
Sensitive data is encrypted in transit and at rest where available in local setup; logs must not include secrets or full personal identifiers.

## Operational Constraints

- Platform scope: generic multi-fund application with web and mobile clients backed by a shared API.
- Environment scope: localhost-first development and testing (`localhost` or loopback address only by default).
- Offline behavior: mobile may queue writes offline, but server remains source of truth; conflict resolution must preserve ledger integrity.
- Time handling: store all timestamps in UTC; display local time in clients.
- Data integrity constraints:
	- No negative contribution balance for member contribution accounts unless an explicit debt model exists.
	- Loan disbursement requires sufficient available fund liquidity at approval time.
	- Repayment allocation order defaults to: due interest, then principal, then advance payment.
	- Closed loans cannot accept new disbursements.
- Reporting constraints:
	- Monthly fund summary includes opening balance, contributions, disbursements, interest earned, repayments, expenses, and closing balance.
	- Every figure in dashboards must tie back to ledger totals.

## Development Workflow and Quality Gates

- Requirement-first: each feature starts with a short functional spec including accounting impact and edge cases.
- Tests required for all financial logic:
	- Unit tests for contribution, disbursement, reducing-balance interest, repayment allocation, and dissolution distribution.
	- Integration tests for end-to-end monthly cycle and multi-fund isolation.
- Any schema/API change affecting calculations or ledgers requires migration notes and backward-compatibility plan.
- Code review checklist must verify: fund scoping, money precision, audit trail integrity, authorization boundaries, and report reproducibility.
- Do not merge if tests for critical financial flows fail.

## Governance

This constitution overrides conflicting local conventions for FundManager.
All pull requests must include a brief constitutional compliance note for affected principles.
Amendments require: (1) proposed change, (2) rationale, (3) migration/impact note, and (4) approval by project owner.
Versioning follows semantic intent at constitution level:
- MAJOR: principle removal/redefinition or governance model change.
- MINOR: new principle/constraint added.
- PATCH: clarifications with no behavioral mandate change.

**Version**: 1.0.0 | **Ratified**: 2026-02-20 | **Last Amended**: 2026-02-20
