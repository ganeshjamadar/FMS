namespace FundManager.Contracts.Events;

/// <summary>
/// Base interface for all integration events (cross-service via MassTransit).
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredAt { get; }
}

// ──────────────────────────────────────────────
// Fund Admin Events
// ──────────────────────────────────────────────

public record FundCreated(
    Guid Id, Guid FundId, string Name, string Currency,
    decimal MonthlyInterestRate, decimal MinimumMonthlyContribution,
    decimal MinimumPrincipalPerRepayment,
    string LoanApprovalPolicy, decimal? MaxLoanPerMember, int? MaxConcurrentLoans,
    DateTime OccurredAt) : IIntegrationEvent;

public record FundActivated(
    Guid Id, Guid FundId, DateTime OccurredAt) : IIntegrationEvent;

public record FundAdminAssigned(
    Guid Id, Guid FundId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;

public record MemberJoined(
    Guid Id, Guid FundId, Guid UserId, Guid MemberPlanId,
    decimal MonthlyContributionAmount, DateTime OccurredAt) : IIntegrationEvent;

public record MemberRemoved(
    Guid Id, Guid FundId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;

public record InvitationSent(
    Guid Id, Guid FundId, Guid InvitationId, string TargetContact,
    DateTime OccurredAt) : IIntegrationEvent;

// ──────────────────────────────────────────────
// Contribution Events
// ──────────────────────────────────────────────

public record ContributionDueGenerated(
    Guid Id, Guid FundId, int MonthYear, int MemberCount,
    decimal TotalAmount, DateTime OccurredAt) : IIntegrationEvent;

public record ContributionPaid(
    Guid Id, Guid FundId, Guid UserId, Guid ContributionDueId,
    decimal AmountPaid, string Status, DateTime OccurredAt) : IIntegrationEvent;

public record ContributionOverdue(
    Guid Id, Guid FundId, Guid UserId, Guid ContributionDueId,
    int MonthYear, string Status, DateTime OccurredAt) : IIntegrationEvent;

// ──────────────────────────────────────────────
// Loan Events
// ──────────────────────────────────────────────

public record LoanRequested(
    Guid Id, Guid FundId, Guid BorrowerId, Guid LoanId,
    decimal PrincipalAmount, DateTime OccurredAt) : IIntegrationEvent;

public record LoanApproved(
    Guid Id, Guid FundId, Guid LoanId, Guid ApprovedBy,
    decimal ScheduledInstallment, DateTime OccurredAt) : IIntegrationEvent;

public record LoanRejected(
    Guid Id, Guid FundId, Guid LoanId, string Reason,
    DateTime OccurredAt) : IIntegrationEvent;

public record LoanDisbursed(
    Guid Id, Guid FundId, Guid LoanId, Guid BorrowerId,
    decimal PrincipalAmount, DateTime OccurredAt) : IIntegrationEvent;

public record LoanClosed(
    Guid Id, Guid FundId, Guid LoanId, DateTime OccurredAt) : IIntegrationEvent;

// ──────────────────────────────────────────────
// Repayment Events
// ──────────────────────────────────────────────

public record RepaymentDueGenerated(
    Guid Id, Guid FundId, Guid LoanId, Guid RepaymentEntryId,
    int MonthYear, decimal InterestDue, decimal PrincipalDue,
    decimal TotalDue, DateTime OccurredAt) : IIntegrationEvent;

public record RepaymentRecorded(
    Guid Id, Guid FundId, Guid LoanId, Guid RepaymentEntryId,
    decimal AmountPaid, decimal InterestPaid, decimal PrincipalPaid,
    decimal ExcessApplied, decimal RemainingBalance,
    DateTime OccurredAt) : IIntegrationEvent;

public record RepaymentOverdue(
    Guid Id, Guid FundId, Guid LoanId, Guid RepaymentEntryId,
    Guid BorrowerId, int MonthYear, decimal AmountDue, decimal AmountPaid,
    int DaysPastDue, DateTime OccurredAt) : IIntegrationEvent;

public record RepaymentPenaltyApplied(
    Guid Id, Guid FundId, Guid LoanId, Guid RepaymentEntryId,
    decimal PenaltyAmount, string PenaltyType,
    DateTime OccurredAt) : IIntegrationEvent;

// ──────────────────────────────────────────────
// Voting Events
// ──────────────────────────────────────────────

public record VotingStarted(
    Guid Id, Guid FundId, Guid LoanId, Guid VotingSessionId,
    DateTime WindowEnd, DateTime OccurredAt) : IIntegrationEvent;

public record VoteCast(
    Guid Id, Guid FundId, Guid VotingSessionId, Guid VoterId,
    string Decision, DateTime OccurredAt) : IIntegrationEvent;

public record VotingFinalised(
    Guid Id, Guid FundId, Guid VotingSessionId, Guid LoanId,
    string Result, bool OverrideUsed, DateTime OccurredAt) : IIntegrationEvent;

// ──────────────────────────────────────────────
// Dissolution Events
// ──────────────────────────────────────────────

public record DissolutionInitiated(
    Guid Id, Guid FundId, Guid InitiatedBy,
    DateTime OccurredAt) : IIntegrationEvent;

public record SettlementCalculated(
    Guid Id, Guid FundId, Guid SettlementId,
    int MemberCount, DateTime OccurredAt) : IIntegrationEvent;

public record DissolutionConfirmed(
    Guid Id, Guid FundId, Guid SettlementId,
    DateTime OccurredAt) : IIntegrationEvent;

// ──────────────────────────────────────────────
// Audit Events
// ──────────────────────────────────────────────

public record AuditLogCreated(
    Guid Id, Guid? FundId, Guid ActorId,
    string EntityType, Guid EntityId, string ActionType,
    string? BeforeState, string? AfterState,
    string? IpAddress, string? UserAgent,
    Guid? CorrelationId, string ServiceName,
    DateTime OccurredAt) : IIntegrationEvent;
