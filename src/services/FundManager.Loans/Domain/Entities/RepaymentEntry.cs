using FundManager.BuildingBlocks.Domain;

namespace FundManager.Loans.Domain.Entities;

/// <summary>
/// Repayment entry lifecycle states.
/// Pending → Paid (full payment) or Partial (partial payment)
/// Pending → Overdue (past due date without full payment)
/// </summary>
public enum RepaymentStatus
{
    Pending,
    Paid,
    Partial,
    Overdue
}

/// <summary>
/// A single monthly repayment entry for a loan, generated using reducing-balance formula.
/// Tracks interest due, principal due, total due, and payment status.
/// Unique constraint: (LoanId, MonthYear) — one entry per loan per month.
/// Concurrency: xmin optimistic locking (FR-035a).
/// </summary>
public class RepaymentEntry : Entity
{
    public Guid LoanId { get; private set; }
    public Guid FundId { get; private set; }
    public int MonthYear { get; private set; }
    public decimal InterestDue { get; private set; }
    public decimal PrincipalDue { get; private set; }
    public decimal TotalDue { get; private set; }
    public decimal AmountPaid { get; private set; }
    public RepaymentStatus Status { get; private set; } = RepaymentStatus.Pending;
    public DateOnly DueDate { get; private set; }
    public DateTime? PaidDate { get; private set; }

    private RepaymentEntry() { } // EF Core

    /// <summary>
    /// Factory: Create a new repayment entry for a given month.
    /// Calculations done externally via MoneyMath and passed in.
    /// </summary>
    public static RepaymentEntry Create(
        Guid loanId,
        Guid fundId,
        int monthYear,
        decimal interestDue,
        decimal principalDue,
        decimal totalDue,
        DateOnly dueDate)
    {
        return new RepaymentEntry
        {
            LoanId = loanId,
            FundId = fundId,
            MonthYear = monthYear,
            InterestDue = interestDue,
            PrincipalDue = principalDue,
            TotalDue = totalDue,
            AmountPaid = 0m,
            Status = RepaymentStatus.Pending,
            DueDate = dueDate
        };
    }

    /// <summary>
    /// Record a payment against this entry. Updates AmountPaid and Status.
    /// </summary>
    public void RecordPayment(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment must be positive.", nameof(amount));

        AmountPaid += amount;

        if (AmountPaid >= TotalDue)
        {
            Status = RepaymentStatus.Paid;
            PaidDate = DateTime.UtcNow;
        }
        else
        {
            Status = RepaymentStatus.Partial;
        }

        SetUpdated();
    }

    /// <summary>
    /// Mark as overdue (called when past due date without full payment).
    /// </summary>
    public void MarkOverdue()
    {
        if (Status == RepaymentStatus.Paid) return;
        Status = RepaymentStatus.Overdue;
        SetUpdated();
    }

    /// <summary>
    /// Add a penalty amount to this entry's total due (FR-073).
    /// </summary>
    public void AddPenalty(decimal penaltyAmount)
    {
        if (penaltyAmount <= 0) return;
        TotalDue += penaltyAmount;
        SetUpdated();
    }
}
