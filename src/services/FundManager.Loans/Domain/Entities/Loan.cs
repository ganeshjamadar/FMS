using FundManager.BuildingBlocks.Domain;

namespace FundManager.Loans.Domain.Entities;

/// <summary>
/// Loan lifecycle states.
/// PendingApproval → Approved → Active → Closed
/// PendingApproval → Rejected
/// </summary>
public enum LoanStatus
{
    PendingApproval,
    Approved,
    Active,
    Closed,
    Rejected
}

/// <summary>
/// A loan requested by a fund member. Tracks principal, repayment terms,
/// and lifecycle state with snapshot fields frozen at approval time.
/// Concurrency: xmin optimistic locking.
/// </summary>
public class Loan : AggregateRoot
{
    public Guid FundId { get; private set; }
    public Guid BorrowerId { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal OutstandingPrincipal { get; private set; }

    // Snapshot fields — captured at approval/disbursement, immutable thereafter
    public decimal MonthlyInterestRate { get; private set; }
    public decimal ScheduledInstallment { get; private set; }
    public decimal MinimumPrincipal { get; private set; } = 1000.00m;

    public int RequestedStartMonth { get; private set; }
    public string? Purpose { get; private set; }
    public LoanStatus Status { get; private set; } = LoanStatus.PendingApproval;
    public Guid? ApprovedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ApprovalDate { get; private set; }
    public DateTime? DisbursementDate { get; private set; }
    public DateTime? ClosedDate { get; private set; }

    private Loan() { } // EF Core

    /// <summary>
    /// Factory: Create a new loan request in PendingApproval status.
    /// </summary>
    public static Loan Create(
        Guid fundId,
        Guid borrowerId,
        decimal principalAmount,
        int requestedStartMonth,
        string? purpose = null)
    {
        if (principalAmount <= 0)
            throw new ArgumentException("Principal must be positive.", nameof(principalAmount));

        return new Loan
        {
            FundId = fundId,
            BorrowerId = borrowerId,
            PrincipalAmount = principalAmount,
            OutstandingPrincipal = principalAmount,
            RequestedStartMonth = requestedStartMonth,
            Purpose = purpose,
            Status = LoanStatus.PendingApproval
        };
    }

    /// <summary>
    /// Approve the loan: set installment, snapshot interest rate and minimum principal from fund config.
    /// Transitions PendingApproval → Approved.
    /// </summary>
    public void Approve(
        Guid approvedBy,
        decimal scheduledInstallment,
        decimal monthlyInterestRate,
        decimal minimumPrincipal)
    {
        if (Status != LoanStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve loan in {Status} status.");

        ApprovedBy = approvedBy;
        ScheduledInstallment = scheduledInstallment;
        MonthlyInterestRate = monthlyInterestRate;
        MinimumPrincipal = minimumPrincipal;
        ApprovalDate = DateTime.UtcNow;
        Status = LoanStatus.Approved;
        SetUpdated();
    }

    /// <summary>
    /// Disburse the loan: transitions Approved → Active and records disbursement date.
    /// </summary>
    public void Disburse()
    {
        if (Status != LoanStatus.Approved)
            throw new InvalidOperationException($"Cannot disburse loan in {Status} status.");

        DisbursementDate = DateTime.UtcNow;
        Status = LoanStatus.Active;
        SetUpdated();
    }

    /// <summary>
    /// Reject the loan with a reason.
    /// Transitions PendingApproval → Rejected.
    /// </summary>
    public void Reject(string reason)
    {
        if (Status != LoanStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot reject loan in {Status} status.");

        RejectionReason = reason;
        Status = LoanStatus.Rejected;
        SetUpdated();
    }

    /// <summary>
    /// Reduce outstanding principal by the given amount.
    /// If principal reaches zero, close the loan.
    /// </summary>
    public void ReducePrincipal(decimal amount)
    {
        if (Status != LoanStatus.Active)
            throw new InvalidOperationException($"Cannot reduce principal on loan in {Status} status.");

        OutstandingPrincipal -= amount;
        if (OutstandingPrincipal <= 0)
        {
            OutstandingPrincipal = 0;
            Status = LoanStatus.Closed;
            ClosedDate = DateTime.UtcNow;
        }
        SetUpdated();
    }
}
