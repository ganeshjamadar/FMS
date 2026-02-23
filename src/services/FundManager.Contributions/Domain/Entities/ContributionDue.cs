using FundManager.BuildingBlocks.Domain;

namespace FundManager.Contributions.Domain.Entities;

public enum ContributionDueStatus
{
    Pending,
    Paid,
    Partial,
    Late,
    Missed
}

/// <summary>
/// Represents a monthly contribution due for a single member in a fund.
/// Unique: (UserId, FundId, MonthYear) — one due per member per fund per month.
/// Concurrency: xmin optimistic locking (FR-035a).
/// </summary>
public class ContributionDue : Entity
{
    public Guid FundId { get; private set; }
    public Guid MemberPlanId { get; private set; }
    public Guid UserId { get; private set; }
    public int MonthYear { get; private set; }
    public decimal AmountDue { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal RemainingBalance { get; private set; }
    public ContributionDueStatus Status { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateTime? PaidDate { get; private set; }

    private ContributionDue() { }

    /// <summary>
    /// Factory: Create a new contribution due for a member/month.
    /// </summary>
    public static ContributionDue Create(
        Guid fundId,
        Guid memberPlanId,
        Guid userId,
        int monthYear,
        decimal amountDue,
        DateOnly dueDate)
    {
        if (amountDue <= 0)
            throw new ArgumentException("Amount due must be greater than zero.", nameof(amountDue));

        return new ContributionDue
        {
            FundId = fundId,
            MemberPlanId = memberPlanId,
            UserId = userId,
            MonthYear = monthYear,
            AmountDue = amountDue,
            AmountPaid = 0,
            RemainingBalance = amountDue,
            Status = ContributionDueStatus.Pending,
            DueDate = dueDate
        };
    }

    /// <summary>
    /// Record a payment against this due. Returns the amount actually applied.
    /// </summary>
    public decimal RecordPayment(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        if (Status == ContributionDueStatus.Missed)
            throw new InvalidOperationException("Cannot record payment for a missed due.");

        var applied = Math.Min(amount, RemainingBalance);
        AmountPaid += applied;
        RemainingBalance = AmountDue - AmountPaid;

        if (RemainingBalance <= 0)
        {
            Status = ContributionDueStatus.Paid;
            PaidDate = DateTime.UtcNow;
        }
        else
        {
            Status = ContributionDueStatus.Partial;
        }

        SetUpdated();
        return applied;
    }

    /// <summary>
    /// FR-033: Mark as Late when grace period expires and still not fully paid.
    /// </summary>
    public Result MarkLate()
    {
        if (Status is ContributionDueStatus.Paid or ContributionDueStatus.Missed)
            return Result.Failure("Cannot mark a paid or missed due as late.", "INVALID_STATE");

        Status = ContributionDueStatus.Late;
        SetUpdated();
        return Result.Success();
    }

    /// <summary>
    /// FR-034: Mark as Missed at month-end when still unpaid/partial.
    /// </summary>
    public Result MarkMissed()
    {
        if (Status == ContributionDueStatus.Paid)
            return Result.Failure("Cannot mark a paid due as missed.", "INVALID_STATE");

        Status = ContributionDueStatus.Missed;
        SetUpdated();
        return Result.Success();
    }
}
