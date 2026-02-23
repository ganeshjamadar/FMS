using FundManager.BuildingBlocks.Domain;

namespace FundManager.Contributions.Domain.Entities;

/// <summary>
/// Local projection of fund member data, maintained via MemberJoined/MemberRemoved events.
/// Used by ContributionCycleService to generate dues without cross-service calls.
/// </summary>
public class MemberProjection : Entity
{
    public Guid FundId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid MemberPlanId { get; private set; }
    public decimal MonthlyContributionAmount { get; private set; }
    public bool IsActive { get; private set; }

    private MemberProjection() { }

    public static MemberProjection Create(
        Guid fundId,
        Guid userId,
        Guid memberPlanId,
        decimal monthlyContributionAmount)
    {
        return new MemberProjection
        {
            FundId = fundId,
            UserId = userId,
            MemberPlanId = memberPlanId,
            MonthlyContributionAmount = monthlyContributionAmount,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }
}
