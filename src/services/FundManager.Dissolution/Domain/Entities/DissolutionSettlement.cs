using FundManager.BuildingBlocks.Domain;

namespace FundManager.Dissolution.Domain.Entities;

public enum DissolutionStatus
{
    Calculating,
    Reviewed,
    Confirmed,
}

/// <summary>
/// Aggregate root for a fund dissolution settlement.
/// One settlement per fund (unique FundId constraint).
/// </summary>
public class DissolutionSettlement : AggregateRoot
{
    public Guid FundId { get; private set; }
    public decimal TotalInterestPool { get; private set; }
    public decimal TotalContributionsCollected { get; private set; }
    public DateOnly? SettlementDate { get; private set; }
    public DissolutionStatus Status { get; private set; }
    public Guid? ConfirmedBy { get; private set; }

    // Navigation
    public IReadOnlyCollection<DissolutionLineItem> LineItems => _lineItems.AsReadOnly();
    private readonly List<DissolutionLineItem> _lineItems = new();

    private DissolutionSettlement() { } // EF Core

    public static DissolutionSettlement Create(Guid fundId)
    {
        var settlement = new DissolutionSettlement
        {
            Id = Guid.NewGuid(),
            FundId = fundId,
            TotalInterestPool = 0m,
            TotalContributionsCollected = 0m,
            Status = DissolutionStatus.Calculating,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        return settlement;
    }

    public void UpdateTotals(decimal totalInterestPool, decimal totalContributionsCollected)
    {
        TotalInterestPool = totalInterestPool;
        TotalContributionsCollected = totalContributionsCollected;
        Status = DissolutionStatus.Reviewed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm(Guid confirmedBy)
    {
        if (Status == DissolutionStatus.Confirmed)
            throw new InvalidOperationException("Settlement is already confirmed.");

        Status = DissolutionStatus.Confirmed;
        ConfirmedBy = confirmedBy;
        SettlementDate = DateOnly.FromDateTime(DateTime.UtcNow);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetForRecalculation()
    {
        if (Status == DissolutionStatus.Confirmed)
            throw new InvalidOperationException("Cannot recalculate a confirmed settlement.");

        Status = DissolutionStatus.Calculating;
        _lineItems.Clear();
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddLineItem(DissolutionLineItem lineItem)
    {
        _lineItems.Add(lineItem);
    }
}
