using FundManager.BuildingBlocks.Audit;
using FundManager.BuildingBlocks.Domain;
using FundManager.Contracts.Events;
using FundManager.Contributions.Domain.Entities;
using FundManager.Contributions.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Domain.Services;

/// <summary>
/// Generates monthly contribution dues for all active members of a fund.
/// Idempotent: re-running for the same month does not create duplicates (FR-030, NFR-011).
/// </summary>
public class ContributionCycleService
{
    private readonly ContributionsDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly AuditEventPublisher _audit;

    public ContributionCycleService(
        ContributionsDbContext db,
        IPublishEndpoint publish,
        AuditEventPublisher audit)
    {
        _db = db;
        _publish = publish;
        _audit = audit;
    }

    /// <summary>
    /// Generate contribution dues for all active members of a fund for a given month.
    /// FR-030: System generates dues at start of each contribution cycle.
    /// FR-031: One due per member per fund per month.
    /// FR-032: Amount comes from MemberContributionPlan (immutable FR-023).
    /// </summary>
    public async Task<Result<(int Generated, int Skipped)>> GenerateDuesAsync(
        Guid fundId,
        int monthYear,
        Guid triggeredBy,
        CancellationToken ct = default)
    {
        // Validate monthYear format (YYYYMM)
        var year = monthYear / 100;
        var month = monthYear % 100;
        if (year < 2020 || year > 2100 || month < 1 || month > 12)
            return Result<(int, int)>.Failure("Invalid monthYear format. Expected YYYYMM.", "VALIDATION_ERROR");

        // Get active members from local projection
        var activeMembers = await _db.MemberProjections
            .AsNoTracking()
            .Where(m => m.FundId == fundId && m.IsActive)
            .ToListAsync(ct);

        if (activeMembers.Count == 0)
            return Result<(int, int)>.Failure("No active members found for this fund.", "NO_MEMBERS");

        // Check which members already have dues for this month (idempotency)
        var existingUserIds = (await _db.ContributionDues
            .AsNoTracking()
            .Where(d => d.FundId == fundId && d.MonthYear == monthYear)
            .Select(d => d.UserId)
            .ToListAsync(ct))
            .ToHashSet();

        var dueDate = new DateOnly(year, month, 1);
        int generated = 0;
        int skipped = 0;

        foreach (var member in activeMembers)
        {
            if (existingUserIds.Contains(member.UserId))
            {
                skipped++;
                continue;
            }

            var due = ContributionDue.Create(
                fundId: fundId,
                memberPlanId: member.MemberPlanId,
                userId: member.UserId,
                monthYear: monthYear,
                amountDue: member.MonthlyContributionAmount,
                dueDate: dueDate);

            _db.ContributionDues.Add(due);
            generated++;
        }

        if (generated > 0)
        {
            await _db.SaveChangesAsync(ct);

            var totalAmount = activeMembers
                .Where(m => !existingUserIds.Contains(m.UserId))
                .Sum(m => m.MonthlyContributionAmount);

            await _publish.Publish(new ContributionDueGenerated(
                Id: Guid.NewGuid(),
                FundId: fundId,
                MonthYear: monthYear,
                MemberCount: generated,
                TotalAmount: totalAmount,
                OccurredAt: DateTime.UtcNow), ct);

            await _audit.PublishAsync(
                fundId: fundId,
                actorId: triggeredBy,
                entityType: "ContributionDue",
                entityId: Guid.Empty,
                actionType: "Dues.Generated",
                beforeState: null,
                afterState: new { MonthYear = monthYear, Generated = generated, Skipped = skipped },
                serviceName: "Contributions",
                cancellationToken: ct);
        }

        return Result<(int, int)>.Success((generated, skipped));
    }
}
