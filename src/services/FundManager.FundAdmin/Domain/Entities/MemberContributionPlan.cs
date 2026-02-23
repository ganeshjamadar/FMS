using FundManager.BuildingBlocks.Domain;

namespace FundManager.FundAdmin.Domain.Entities;

/// <summary>
/// Tracks a member's contribution plan within a fund.
/// Unique: one plan per user per fund.
/// MonthlyContributionAmount is immutable after creation (FR-023).
/// </summary>
public class MemberContributionPlan : Entity
{
    public Guid UserId { get; private set; }
    public Guid FundId { get; private set; }
    public decimal MonthlyContributionAmount { get; private set; }
    public DateOnly JoinDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public Fund Fund { get; private set; } = null!;

    private MemberContributionPlan() { } // EF Core

    public static MemberContributionPlan Create(
        Guid userId, Guid fundId, decimal monthlyContributionAmount, DateOnly? joinDate = null)
    {
        if (monthlyContributionAmount <= 0)
            throw new ArgumentException("Monthly contribution amount must be > 0.");

        return new MemberContributionPlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FundId = fundId,
            MonthlyContributionAmount = monthlyContributionAmount,
            JoinDate = joinDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}
