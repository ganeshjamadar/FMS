using FundManager.BuildingBlocks.Domain;
using FundManager.BuildingBlocks.Financial;
using FundManager.BuildingBlocks.Audit;
using FundManager.Contracts.Events;
using FundManager.Dissolution.Domain.Entities;
using FundManager.Dissolution.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Dissolution.Domain.Services;

/// <summary>
/// Manages the full dissolution lifecycle: initiate, calculate settlement, confirm.
/// </summary>
public class DissolutionService
{
    private readonly DissolutionDbContext _db;
    private readonly IPublishEndpoint _publisher;
    private readonly AuditEventPublisher _audit;

    public DissolutionService(
        DissolutionDbContext db,
        IPublishEndpoint publisher,
        AuditEventPublisher audit)
    {
        _db = db;
        _publisher = publisher;
        _audit = audit;
    }

    /// <summary>
    /// Initiates dissolution for a fund.
    /// Creates settlement record and publishes DissolutionInitiated event.
    /// </summary>
    public async Task<Result<DissolutionSettlement>> InitiateDissolutionAsync(
        Guid fundId, Guid initiatedBy, CancellationToken ct = default)
    {
        // Check if dissolution already in progress
        var existing = await _db.DissolutionSettlements
            .FirstOrDefaultAsync(s => s.FundId == fundId, ct);

        if (existing is not null)
            return Result<DissolutionSettlement>.Failure("Dissolution already in progress for this fund.", "CONFLICT");

        var settlement = DissolutionSettlement.Create(fundId);
        _db.DissolutionSettlements.Add(settlement);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new DissolutionInitiated(
            Id: Guid.NewGuid(),
            FundId: fundId,
            InitiatedBy: initiatedBy,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: initiatedBy,
            entityType: "DissolutionSettlement",
            entityId: settlement.Id,
            actionType: "DissolutionInitiated",
            beforeState: null,
            afterState: new { settlement.Status },
            serviceName: "FundManager.Dissolution",
            cancellationToken: ct);

        return Result<DissolutionSettlement>.Success(settlement);
    }

    /// <summary>
    /// Calculates settlement based on member projections stored locally.
    /// </summary>
    public async Task<Result<DissolutionSettlement>> CalculateSettlementAsync(
        Guid fundId, CancellationToken ct = default)
    {
        var settlement = await _db.DissolutionSettlements
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync(s => s.FundId == fundId, ct);

        if (settlement is null)
            return Result<DissolutionSettlement>.Failure("No dissolution in progress.", "NOT_FOUND");

        if (settlement.Status == DissolutionStatus.Confirmed)
            return Result<DissolutionSettlement>.Failure("Settlement already confirmed.", "CONFLICT");

        // Reset for fresh calculation
        settlement.ResetForRecalculation();

        // Remove existing line items from DB
        var existingItems = await _db.DissolutionLineItems
            .Where(li => li.SettlementId == settlement.Id)
            .ToListAsync(ct);
        _db.DissolutionLineItems.RemoveRange(existingItems);

        // Fetch member projections (populated by MemberJoined events)
        var members = await _db.MemberProjections
            .Where(m => m.FundId == fundId)
            .ToListAsync(ct);

        if (members.Count == 0)
        {
            settlement.UpdateTotals(0m, 0m);
            await _db.SaveChangesAsync(ct);
            return Result<DissolutionSettlement>.Success(settlement);
        }

        // Fetch loan projections (populated by Loan events)
        var loans = await _db.LoanProjections
            .Where(l => l.FundId == fundId)
            .ToListAsync(ct);

        // Fetch contribution projections (populated by ContributionPaid events)
        var contributions = await _db.ContributionProjections
            .Where(c => c.FundId == fundId)
            .ToListAsync(ct);

        // Fetch interest income projections
        var interestIncome = await _db.InterestIncomeProjections
            .Where(i => i.FundId == fundId)
            .ToListAsync(ct);

        var totalInterestPool = interestIncome.Sum(i => i.Amount);
        var totalContributions = contributions.Sum(c => c.TotalPaid);
        var totalWeight = members.Sum(m => m.MonthlyContributionAmount);

        foreach (var member in members)
        {
            var memberContributions = contributions
                .Where(c => c.UserId == member.UserId)
                .Sum(c => c.TotalPaid);

            // Interest share: proportional by weight (monthly contribution amount)
            var interestShare = totalWeight > 0
                ? MoneyMath.Round(totalInterestPool * (member.MonthlyContributionAmount / totalWeight))
                : 0m;

            // Outstanding loans for this member
            var memberLoans = loans.Where(l => l.BorrowerId == member.UserId).ToList();
            var outstandingPrincipal = memberLoans.Sum(l => l.OutstandingPrincipal);
            var unpaidInterest = memberLoans.Sum(l => l.UnpaidInterest);

            // Unpaid contribution dues
            var unpaidDues = contributions
                .Where(c => c.UserId == member.UserId)
                .Sum(c => c.UnpaidAmount);

            var lineItem = DissolutionLineItem.Create(
                settlementId: settlement.Id,
                userId: member.UserId,
                totalPaidContributions: memberContributions,
                interestShare: interestShare,
                outstandingLoanPrincipal: outstandingPrincipal,
                unpaidInterest: unpaidInterest,
                unpaidDues: unpaidDues);

            settlement.AddLineItem(lineItem);
            _db.DissolutionLineItems.Add(lineItem);
        }

        settlement.UpdateTotals(totalInterestPool, totalContributions);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new SettlementCalculated(
            Id: Guid.NewGuid(),
            FundId: fundId,
            SettlementId: settlement.Id,
            MemberCount: members.Count,
            OccurredAt: DateTime.UtcNow), ct);

        return Result<DissolutionSettlement>.Success(settlement);
    }

    /// <summary>
    /// Confirms dissolution. Blocked if any member has negative net payout.
    /// </summary>
    public async Task<Result<DissolutionSettlement>> ConfirmDissolutionAsync(
        Guid fundId, Guid confirmedBy, CancellationToken ct = default)
    {
        var settlement = await _db.DissolutionSettlements
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync(s => s.FundId == fundId, ct);

        if (settlement is null)
            return Result<DissolutionSettlement>.Failure("No dissolution in progress.", "NOT_FOUND");

        if (settlement.Status == DissolutionStatus.Confirmed)
            return Result<DissolutionSettlement>.Failure("Settlement already confirmed.", "CONFLICT");

        if (settlement.Status == DissolutionStatus.Calculating)
            return Result<DissolutionSettlement>.Failure("Settlement must be calculated before confirming.", "VALIDATION");

        // Block if any member has negative net payout (FR-083)
        var negativePayouts = settlement.LineItems.Where(li => li.NetPayout < 0).ToList();
        if (negativePayouts.Count > 0)
        {
            return Result<DissolutionSettlement>.Failure(
                $"{negativePayouts.Count} member(s) have negative net payout. Resolve outstanding obligations first.",
                "CONFLICT");
        }

        var beforeState = new { settlement.Status };
        settlement.Confirm(confirmedBy);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new DissolutionConfirmed(
            Id: Guid.NewGuid(),
            FundId: fundId,
            SettlementId: settlement.Id,
            OccurredAt: DateTime.UtcNow), ct);

        await _audit.PublishAsync(
            fundId: fundId,
            actorId: confirmedBy,
            entityType: "DissolutionSettlement",
            entityId: settlement.Id,
            actionType: "DissolutionConfirmed",
            beforeState: beforeState,
            afterState: new { settlement.Status, settlement.SettlementDate },
            serviceName: "FundManager.Dissolution",
            cancellationToken: ct);

        return Result<DissolutionSettlement>.Success(settlement);
    }

    /// <summary>
    /// Gets the settlement detail for a fund.
    /// </summary>
    public async Task<Result<DissolutionSettlement>> GetSettlementAsync(
        Guid fundId, CancellationToken ct = default)
    {
        var settlement = await _db.DissolutionSettlements
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync(s => s.FundId == fundId, ct);

        if (settlement is null)
            return Result<DissolutionSettlement>.Failure("No dissolution in progress.", "NOT_FOUND");

        return Result<DissolutionSettlement>.Success(settlement);
    }
}
