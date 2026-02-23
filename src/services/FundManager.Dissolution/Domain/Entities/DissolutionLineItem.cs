using FundManager.BuildingBlocks.Domain;

namespace FundManager.Dissolution.Domain.Entities;

/// <summary>
/// Per-member line item in a dissolution settlement.
/// Unique per (SettlementId, UserId).
/// </summary>
public class DissolutionLineItem : Entity
{
    public Guid SettlementId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal TotalPaidContributions { get; private set; }
    public decimal InterestShare { get; private set; }
    public decimal OutstandingLoanPrincipal { get; private set; }
    public decimal UnpaidInterest { get; private set; }
    public decimal UnpaidDues { get; private set; }
    public decimal GrossPayout { get; private set; }
    public decimal NetPayout { get; private set; }

    private DissolutionLineItem() { } // EF Core

    public static DissolutionLineItem Create(
        Guid settlementId,
        Guid userId,
        decimal totalPaidContributions,
        decimal interestShare,
        decimal outstandingLoanPrincipal,
        decimal unpaidInterest,
        decimal unpaidDues)
    {
        var grossPayout = totalPaidContributions + interestShare;
        var netPayout = grossPayout - outstandingLoanPrincipal - unpaidInterest - unpaidDues;

        return new DissolutionLineItem
        {
            Id = Guid.NewGuid(),
            SettlementId = settlementId,
            UserId = userId,
            TotalPaidContributions = totalPaidContributions,
            InterestShare = interestShare,
            OutstandingLoanPrincipal = outstandingLoanPrincipal,
            UnpaidInterest = unpaidInterest,
            UnpaidDues = unpaidDues,
            GrossPayout = grossPayout,
            NetPayout = netPayout,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
